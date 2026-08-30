param(
    [string]$Configuration = 'Debug',
    [ValidateSet('All', 'Startup', 'PetLoop')]
    [string]$Focus = 'All'
)

$ErrorActionPreference = 'Stop'

function Assert-Equal([object]$Expected, [object]$Actual, [string]$Message)
{
    if ($Expected -ne $Actual)
    {
        throw "$Message Expected '$Expected', observed '$Actual'."
    }
}

function Assert-True([bool]$Condition, [string]$Message)
{
    if (-not $Condition)
    {
        throw $Message
    }
}

function Get-RequiredType([System.Reflection.Assembly]$Assembly, [string]$Name)
{
    $type = $Assembly.GetType($Name, $false)
    if ($null -eq $type)
    {
        throw "Required runtime-composition seam '$Name' was not found."
    }

    return $type
}

function New-InternalInstance([Type]$Type, [object[]]$Arguments = @())
{
    return [Activator]::CreateInstance(
        $Type,
        [Reflection.BindingFlags]'Instance,Public,NonPublic',
        $null,
        $Arguments,
        $null)
}

function Get-RequiredMethod([Type]$Type, [string]$Name)
{
    $method = $Type.GetMethod(
        $Name,
        [Reflection.BindingFlags]'Static,Instance,Public,NonPublic')
    if ($null -eq $method)
    {
        throw "Required method '$($Type.FullName).$Name' was not found."
    }

    return $method
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$coreAssemblyPath = Join-Path $repositoryRoot "src/Dororong.Core/bin/$Configuration/net8.0/Dororong.Core.dll"
$appAssemblyPath = Join-Path $repositoryRoot "src/Dororong.App/bin/$Configuration/net8.0-windows/Dororong.App.dll"

Add-Type -AssemblyName PresentationFramework
Add-Type -Path $coreAssemblyPath
Add-Type -Path $appAssemblyPath

$appAssembly = [Reflection.Assembly]::LoadFrom($appAssemblyPath)
$oneShotType = Get-RequiredType $appAssembly 'Dororong.App.Runtime.OneShotOperation'
$lifecycleType = Get-RequiredType $appAssembly 'Dororong.App.Runtime.PetLoopLifecycle'
$cleanupType = Get-RequiredType $appAssembly 'Dororong.App.Runtime.CleanupSequence'
$fatalBoundaryType = Get-RequiredType $appAssembly 'Dororong.App.Runtime.FatalBoundary'
$pressQueueType = Get-RequiredType $appAssembly 'Dororong.App.Runtime.BodyPressQueue'
$captureTransitionType = Get-RequiredType $appAssembly 'Dororong.App.Runtime.MouseCaptureTransition'

# Removing the one-shot guard, or allowing a failed handler to retry, must fail this check.
$oneShot = New-InternalInstance $oneShotType
$tryRun = Get-RequiredMethod $oneShotType 'TryRun'
$runCount = 0
$firstRun = $tryRun.Invoke($oneShot, @([Action]{ $script:runCount++ }))
$secondRun = $tryRun.Invoke($oneShot, @([Action]{ $script:runCount++ }))
Assert-True $firstRun 'The first Loaded-style initialization was rejected.'
Assert-True (-not $secondRun) 'Repeated Loaded-style initialization was accepted.'
Assert-Equal 1 $runCount 'Loaded-style initialization did not run exactly once.'

$failedOneShot = New-InternalInstance $oneShotType
$failedRunCount = 0
$injectedFailureObserved = $false
try
{
    $null = $tryRun.Invoke(
        $failedOneShot,
        @([Action]{ $script:failedRunCount++; throw 'injected fatal handler failure' }))
}
catch
{
    $observedException = $_.Exception
    while ($null -ne $observedException.InnerException)
    {
        $observedException = $observedException.InnerException
    }

    Assert-Equal 'injected fatal handler failure' $observedException.Message 'Wrong one-shot failure propagated.'
    $injectedFailureObserved = $true
}

Assert-True $injectedFailureObserved 'The injected one-shot failure did not propagate.'
$retryAfterFailure = $tryRun.Invoke($failedOneShot, @([Action]{ $script:failedRunCount++ }))
Assert-True (-not $retryAfterFailure) 'A failed one-shot operation was retried.'
Assert-Equal 1 $failedRunCount 'A failed fatal-style handler did not remain one-shot.'

# Removing any Created/Running/Faulted/Disposed transition guard must fail this check.
$lifecycle = New-InternalInstance $lifecycleType
$phase = $lifecycleType.GetProperty('Phase')
$tryStart = Get-RequiredMethod $lifecycleType 'TryStart'
$tryFault = Get-RequiredMethod $lifecycleType 'TryFault'
$tryDispose = Get-RequiredMethod $lifecycleType 'TryDispose'
Assert-Equal 'Created' $phase.GetValue($lifecycle).ToString() 'A new loop had the wrong lifecycle phase.'
Assert-True $tryStart.Invoke($lifecycle, @()) 'The first Start was rejected.'
Assert-True (-not $tryStart.Invoke($lifecycle, @())) 'A second Start was accepted while running.'
Assert-True $tryFault.Invoke($lifecycle, @()) 'The first fault transition was rejected.'
Assert-True (-not $tryFault.Invoke($lifecycle, @())) 'Faulted was emitted more than once.'
Assert-True (-not $tryStart.Invoke($lifecycle, @())) 'Start was accepted after a fault.'
Assert-True $tryDispose.Invoke($lifecycle, @()) 'The first Dispose transition was rejected.'
Assert-True (-not $tryDispose.Invoke($lifecycle, @())) 'Dispose was not idempotent.'
Assert-True (-not $tryStart.Invoke($lifecycle, @())) 'Start was accepted after disposal.'
Assert-Equal 'Disposed' $phase.GetValue($lifecycle).ToString() 'A disposed loop had the wrong lifecycle phase.'

# Removing best-effort continuation after one cleanup failure must fail this check.
$cleanupTrace = [Collections.Generic.List[string]]::new()
$cleanupSteps = [Action[]]@(
    [Action]{ $cleanupTrace.Add('timer') | Out-Null; throw 'timer cleanup failed' },
    [Action]{ $cleanupTrace.Add('clock') | Out-Null },
    [Action]{ $cleanupTrace.Add('capture') | Out-Null; throw 'capture cleanup failed' },
    [Action]{ $cleanupTrace.Add('fields') | Out-Null }
)
$runCleanup = Get-RequiredMethod $cleanupType 'Run'
$cleanupError = $runCleanup.Invoke($null, [object[]]@(,$cleanupSteps))
Assert-Equal 'timer,clock,capture,fields' ($cleanupTrace -join ',') 'Cleanup stopped before attempting every step.'
Assert-True ($cleanupError -is [AggregateException]) 'Multiple cleanup errors were not preserved as an AggregateException.'
Assert-Equal 2 $cleanupError.InnerExceptions.Count 'The cleanup AggregateException lost an injected failure.'

# Removing the finally-equivalent shutdown or attempting the error message twice must fail this check.
$fatalTrace = [Collections.Generic.List[string]]::new()
$displayedFatal = $null
$primaryFatal = [InvalidOperationException]::new('primary failure')
$fatalCleanup = [Func[Exception]]{
    $fatalTrace.Add('cleanup') | Out-Null
    return [InvalidOperationException]::new('fatal cleanup failed')
}
$showFatal = [Action[Exception]]{
    param($fatal)
    $script:displayedFatal = $fatal
    $fatalTrace.Add('message') | Out-Null
    throw 'message display failed'
}
$shutdown = [Action]{ $fatalTrace.Add('shutdown') | Out-Null }
$runFatal = Get-RequiredMethod $fatalBoundaryType 'Run'
$runFatal.Invoke($null, @($primaryFatal, $fatalCleanup, $showFatal, $shutdown))
Assert-Equal 'cleanup,message,shutdown' ($fatalTrace -join ',') 'Fatal cleanup/message/shutdown ordering was not preserved.'
Assert-True ($displayedFatal -is [AggregateException]) 'The fatal message did not receive the primary and cleanup failures.'
Assert-Equal 2 $displayedFatal.InnerExceptions.Count 'The fatal AggregateException lost the primary or cleanup failure.'

# Clearing or repeatedly returning the queued press must fail this fast-click preservation check.
$pressQueue = New-InternalInstance $pressQueueType
$enqueue = Get-RequiredMethod $pressQueueType 'Enqueue'
$consume = Get-RequiredMethod $pressQueueType 'Consume'
$press = [Dororong.Core.Geometry.PointD]::new(123.5, 456.25)
$enqueue.Invoke($pressQueue, @($press))
$firstPress = $consume.Invoke($pressQueue, @())
$secondPress = $consume.Invoke($pressQueue, @())
Assert-Equal $press $firstPress 'The queued body press was not preserved for the next tick.'
Assert-True ($null -eq $secondPress) 'The queued body press was consumed more than once.'

# Capturing while merely pending, recapturing, or failing to release must fail these state transitions.
$decideCapture = Get-RequiredMethod $captureTransitionType 'Decide'
$petState = [Dororong.Core.Behavior.PetState]
Assert-Equal 'None' $decideCapture.Invoke($null, @($petState::Idle, $petState::Idle)).ToString() 'A non-drag transition requested capture.'
Assert-Equal 'Capture' $decideCapture.Invoke($null, @($petState::Idle, $petState::Dragged)).ToString() 'First DRAGGED entry did not request capture.'
Assert-Equal 'None' $decideCapture.Invoke($null, @($petState::Dragged, $petState::Dragged)).ToString() 'A continuing drag requested capture again.'
Assert-Equal 'Release' $decideCapture.Invoke($null, @($petState::Dragged, $petState::Idle)).ToString() 'DRAGGED exit did not request release.'

if ($Focus -in @('All', 'Startup'))
{
    $startupSequenceType = Get-RequiredType $appAssembly 'Dororong.App.Runtime.AppStartupSequence'
    $startupTryRun = Get-RequiredMethod $startupSequenceType 'TryRun'
    $startupTryHandleFatal = Get-RequiredMethod $startupSequenceType 'TryHandleFatal'

    $normalStartup = New-InternalInstance $startupSequenceType
    $normalTrace = [Collections.Generic.List[string]]::new()
    $mainWindowToken = [object]::new()
    $createMainWindow = [Func[object]]{
        $normalTrace.Add('create') | Out-Null
        return $mainWindowToken
    }
    $setMainWindow = [Action[object]]{
        param($window)
        Assert-True ([object]::ReferenceEquals($mainWindowToken, $window)) 'Startup assigned a different main-window instance.'
        $normalTrace.Add('set') | Out-Null
    }
    $showMainWindow = [Action[object]]{
        param($window)
        Assert-True ([object]::ReferenceEquals($mainWindowToken, $window)) 'Startup showed a different main-window instance.'
        $normalTrace.Add('show') | Out-Null
    }
    $noCleanup = [Func[Exception]]{ return $null }
    $unexpectedFatal = [Action[Exception]]{ throw 'Normal startup reached the fatal handler.' }
    $normalShutdown = [Action[int]]{ param($code) throw "Normal startup requested shutdown $code." }

    $firstStartup = $startupTryRun.Invoke(
        $normalStartup,
        @($createMainWindow, $setMainWindow, $showMainWindow, $noCleanup, $unexpectedFatal, $normalShutdown))
    $secondStartup = $startupTryRun.Invoke(
        $normalStartup,
        @($createMainWindow, $setMainWindow, $showMainWindow, $noCleanup, $unexpectedFatal, $normalShutdown))

    Assert-True $firstStartup 'The first application startup was rejected.'
    Assert-True (-not $secondStartup) 'Application startup ran more than once.'
    Assert-Equal 'create,set,show' ($normalTrace -join ',') 'Normal startup did not create, assign, and show exactly once in order.'

    $fatalStartup = New-InternalInstance $startupSequenceType
    $fatalTrace = [Collections.Generic.List[string]]::new()
    $fatalShown = 0
    $shutdownCode = $null
    $createFailure = [Func[object]]{
        $fatalTrace.Add('create') | Out-Null
        throw [InvalidOperationException]::new('startup construction failed')
    }
    $unusedWindowAction = [Action[object]]{ param($window) throw 'A failed startup continued to a window action.' }
    $startupCleanup = [Func[Exception]]{
        $fatalTrace.Add('cleanup') | Out-Null
        return $null
    }
    $showStartupError = [Action[Exception]]{
        param($exception)
        $script:fatalShown++
        $fatalTrace.Add('message') | Out-Null
        throw 'injected message failure'
    }
    $shutdownStartup = [Action[int]]{
        param($code)
        $script:shutdownCode = $code
        $fatalTrace.Add("shutdown:$code") | Out-Null
    }

    $startupTryRun.Invoke(
        $fatalStartup,
        @($createFailure, $unusedWindowAction, $unusedWindowAction, $startupCleanup, $showStartupError, $shutdownStartup)) | Out-Null
    $secondFatal = $startupTryHandleFatal.Invoke(
        $fatalStartup,
        @([InvalidOperationException]::new('dispatcher follow-up'), $startupCleanup, $showStartupError, $shutdownStartup))

    Assert-Equal 'create,cleanup,message,shutdown:1' ($fatalTrace -join ',') 'Startup fatal did not clean up, try one ordinary error, then shut down with exit code 1.'
    Assert-Equal 1 $fatalShown 'Startup fatal attempted to show an ordinary error more than once.'
    Assert-Equal 1 $shutdownCode 'Startup fatal did not request exit code 1.'
    Assert-True (-not $secondFatal) 'A dispatcher follow-up escaped the startup fatal one-shot boundary.'
}

function New-PetLoopFixture(
    [Type]$ClockType,
    [Type]$TimerType,
    [Type]$HostType,
    [Type]$LoopType)
{
    $state = @{
        Elapsed = [TimeSpan]::Zero
        Tick = $null
        TimerStartCount = 0
        TimerStopCount = 0
        TimerDetachCount = 0
        ClockStartCount = 0
        ClockStopCount = 0
        CaptureCount = 0
        ReleaseCount = 0
        FaultCount = 0
        FailInput = $false
        FailRender = $false
        FailCapture = $false
        FailTimerStop = $false
        Pointer = [Dororong.Core.Behavior.PointerSample]::Unavailable
        PrimaryButtonDown = $false
        WindowPosition = [Dororong.Core.Geometry.PointD]::new(0, 0)
        LastSnapshot = $null
        Trace = [Collections.Generic.List[string]]::new()
    }

    $getElapsedBlock = { return $state.Elapsed }.GetNewClosure()
    $startClockBlock = { $state.ClockStartCount++ }.GetNewClosure()
    $stopClockBlock = {
        $state.ClockStopCount++
        $state.Trace.Add('clock-stop') | Out-Null
    }.GetNewClosure()
    $clock = New-InternalInstance $ClockType ([object[]]@(
        [Func[TimeSpan]]$getElapsedBlock,
        [Action]$startClockBlock,
        [Action]$stopClockBlock))

    $attachTickBlock = { param($handler) $state.Tick = $handler }.GetNewClosure()
    $detachTickBlock = {
        param($handler)
        $state.TimerDetachCount++
        $state.Trace.Add('timer-detach') | Out-Null
    }.GetNewClosure()
    $startTimerBlock = { $state.TimerStartCount++ }.GetNewClosure()
    $stopTimerBlock = {
        $state.TimerStopCount++
        $state.Trace.Add('timer-stop') | Out-Null
        if ($state.FailTimerStop)
        {
            throw 'injected timer stop failure'
        }
    }.GetNewClosure()
    $timer = New-InternalInstance $TimerType ([object[]]@(
        [Action[EventHandler]]$attachTickBlock,
        [Action[EventHandler]]$detachTickBlock,
        [Action]$startTimerBlock,
        [Action]$stopTimerBlock))

    $getWorkArea = [Func[Dororong.Core.Geometry.RectD]]{
        return [Dororong.Core.Geometry.RectD]::new(0, 0, 800, 600)
    }
    $getPetSize = [Func[Dororong.Core.Geometry.SizeD]]{
        return [Dororong.Core.Geometry.SizeD]::new(120, 100)
    }
    $getDragThreshold = [Func[Dororong.Core.Geometry.SizeD]]{
        return [Dororong.Core.Geometry.SizeD]::new(4, 4)
    }
    $samplePointerBlock = {
        $state.Trace.Add('input') | Out-Null
        if ($state.FailInput)
        {
            throw 'injected input failure'
        }

        return $state.Pointer
    }.GetNewClosure()
    $isPrimaryDownBlock = { return $state.PrimaryButtonDown }.GetNewClosure()
    $getWindowPositionBlock = { return $state.WindowPosition }.GetNewClosure()
    $setWindowPositionBlock = {
        param($position)
        $state.WindowPosition = $position
        $state.Trace.Add('position') | Out-Null
    }.GetNewClosure()
    $renderBlock = {
        param($snapshot)
        $state.Trace.Add('render') | Out-Null
        if ($state.FailRender)
        {
            throw 'injected render failure'
        }

        $state.LastSnapshot = $snapshot
    }.GetNewClosure()
    $captureBlock = {
        $state.CaptureCount++
        $state.Trace.Add('capture') | Out-Null
        return -not $state.FailCapture
    }.GetNewClosure()
    $releaseBlock = {
        $state.ReleaseCount++
        $state.Trace.Add('release') | Out-Null
    }.GetNewClosure()
    $loopHost = New-InternalInstance $HostType ([object[]]@(
        $getWorkArea,
        $getPetSize,
        $getDragThreshold,
        [Func[Dororong.Core.Behavior.PointerSample]]$samplePointerBlock,
        [Func[bool]]$isPrimaryDownBlock,
        [Func[Dororong.Core.Geometry.PointD]]$getWindowPositionBlock,
        [Action[Dororong.Core.Geometry.PointD]]$setWindowPositionBlock,
        [Action[Dororong.Core.Behavior.PetSnapshot]]$renderBlock,
        [Func[bool]]$captureBlock,
        [Action]$releaseBlock))

    $brainFactory = [Func[Dororong.Core.Geometry.PointD,Dororong.Core.Behavior.PetBrain]]{
        param($initialPosition)
        return [Dororong.Core.Behavior.PetBrain]::new(
            [Dororong.Core.Behavior.BehaviorTuning]::Default,
            [Dororong.Core.Behavior.SeededRandomSource]::new(1),
            $initialPosition)
    }
    $loop = New-InternalInstance $LoopType ([object[]]@($clock, $timer, $loopHost, $brainFactory))
    $faultHandlerBlock = {
        param($sender, $exception)
        $state.FaultCount++
        $state.LastFault = $exception
    }.GetNewClosure()
    $faultHandler = [EventHandler[Exception]]$faultHandlerBlock
    $LoopType.GetEvent('Faulted').AddEventHandler($loop, $faultHandler)

    return [pscustomobject]@{
        Loop = $loop
        State = $state
        Start = Get-RequiredMethod $LoopType 'Start'
        Notify = Get-RequiredMethod $LoopType 'NotifyBodyPressed'
        Dispose = Get-RequiredMethod $LoopType 'Dispose'
    }
}

function Invoke-PetLoopTick($Fixture, [double]$Seconds)
{
    $Fixture.State.Elapsed = [TimeSpan]::FromSeconds($Seconds)
    $Fixture.State.Tick.Invoke($null, [EventArgs]::Empty)
}

if ($Focus -in @('All', 'PetLoop'))
{
    $clockType = Get-RequiredType $appAssembly 'Dororong.App.Runtime.PetLoopClock'
    $timerType = Get-RequiredType $appAssembly 'Dororong.App.Runtime.PetLoopTimer'
    $hostType = Get-RequiredType $appAssembly 'Dororong.App.Runtime.PetLoopHost'
    $petLoopType = Get-RequiredType $appAssembly 'Dororong.App.PetLoop'
    $tickIntervalField = $petLoopType.GetField(
        'TickInterval',
        [Reflection.BindingFlags]'Static,NonPublic')
    Assert-True ($null -ne $tickIntervalField) 'The production PetLoop timer interval field was not found.'
    $tickInterval = [TimeSpan]$tickIntervalField.GetValue($null)
    Assert-True ($tickInterval.TotalMilliseconds -le 16.7) `
        "The production PetLoop timer interval exceeded the display-cadence limit. Expected <= '16.7 ms', observed '$($tickInterval.TotalMilliseconds) ms'."

    $fixture = New-PetLoopFixture $clockType $timerType $hostType $petLoopType
    $fixture.Start.Invoke($fixture.Loop, @()) | Out-Null
    Assert-Equal 1 $fixture.State.TimerStartCount 'PetLoop.Start did not start its timer exactly once.'
    Assert-True ($null -ne $fixture.State.Tick) 'PetLoop.Start did not attach a tick callback.'
    Assert-Equal ([Dororong.Core.Geometry.PointD]::new(648, 468)) $fixture.State.WindowPosition 'PetLoop.Start chose the wrong initial window position.'

    $fixture.State.Trace.Clear()
    $fixture.State.Pointer = [Dororong.Core.Behavior.PointerSample]::new(
        $true,
        [Dororong.Core.Geometry.PointD]::new(708, 518))
    $fixture.State.PrimaryButtonDown = $false
    $fixture.Notify.Invoke(
        $fixture.Loop,
        @([Dororong.Core.Geometry.PointD]::new(60, 50))) | Out-Null
    Invoke-PetLoopTick $fixture 0.033

    Assert-Equal 'input,position,render' ($fixture.State.Trace -join ',') 'PetLoop tick did not apply input, brain result, window position, then render in order.'
    Assert-Equal 'ClickReaction' $fixture.State.LastSnapshot.State.ToString() 'The queued press did not reach the real brain as one fast click.'
    Assert-Equal ([Dororong.Core.Geometry.PointD]::new(648, 468)) $fixture.State.WindowPosition 'The click tick applied the wrong brain position.'

    $fixture.State.Trace.Clear()
    Invoke-PetLoopTick $fixture 0.066
    Assert-True (-not $fixture.State.LastSnapshot.IsDirectInteractionPending) 'The queued press was delivered to the brain more than once.'

    $drag = New-PetLoopFixture $clockType $timerType $hostType $petLoopType
    $drag.Start.Invoke($drag.Loop, @()) | Out-Null
    $drag.State.Pointer = [Dororong.Core.Behavior.PointerSample]::new(
        $true,
        [Dororong.Core.Geometry.PointD]::new(708, 518))
    $drag.State.PrimaryButtonDown = $true
    $drag.Notify.Invoke($drag.Loop, @([Dororong.Core.Geometry.PointD]::new(60, 50))) | Out-Null
    Invoke-PetLoopTick $drag 0.033
    $drag.State.Pointer = [Dororong.Core.Behavior.PointerSample]::new(
        $true,
        [Dororong.Core.Geometry.PointD]::new(713, 518))
    Invoke-PetLoopTick $drag 0.066
    Assert-Equal 'Dragged' $drag.State.LastSnapshot.State.ToString() 'Threshold crossing did not enter DRAGGED through the real loop and brain.'
    Assert-Equal 1 $drag.State.CaptureCount 'DRAGGED entry did not capture exactly once.'
    Assert-Equal ([Dororong.Core.Geometry.PointD]::new(653, 468)) $drag.State.WindowPosition 'DRAGGED did not preserve the grab offset in the applied window position.'
    Invoke-PetLoopTick $drag 0.099
    Assert-Equal 1 $drag.State.CaptureCount 'A continuing DRAGGED tick captured again.'
    $drag.State.PrimaryButtonDown = $false
    Invoke-PetLoopTick $drag 0.132
    Assert-Equal 1 $drag.State.ReleaseCount 'Leaving DRAGGED did not release capture.'

    foreach ($failureKind in @('input', 'render', 'capture'))
    {
        $faulted = New-PetLoopFixture $clockType $timerType $hostType $petLoopType
        $faulted.Start.Invoke($faulted.Loop, @()) | Out-Null

        if ($failureKind -eq 'capture')
        {
            $faulted.State.Pointer = [Dororong.Core.Behavior.PointerSample]::new(
                $true,
                [Dororong.Core.Geometry.PointD]::new(708, 518))
            $faulted.State.PrimaryButtonDown = $true
            $faulted.Notify.Invoke($faulted.Loop, @([Dororong.Core.Geometry.PointD]::new(60, 50))) | Out-Null
            Invoke-PetLoopTick $faulted 0.033
            $faulted.State.Pointer = [Dororong.Core.Behavior.PointerSample]::new(
                $true,
                [Dororong.Core.Geometry.PointD]::new(713, 518))
            $faulted.State.FailCapture = $true
            Invoke-PetLoopTick $faulted 0.066
        }
        else
        {
            $faulted.State.FailInput = $failureKind -eq 'input'
            $faulted.State.FailRender = $failureKind -eq 'render'
            Invoke-PetLoopTick $faulted 0.033
        }

        Assert-Equal 1 $faulted.State.FaultCount "$failureKind failure did not emit Faulted exactly once."
        Assert-Equal 1 $faulted.State.TimerDetachCount "$failureKind failure did not detach the timer callback."
        $timerStartsBeforeRestart = $faulted.State.TimerStartCount
        $faulted.Start.Invoke($faulted.Loop, @()) | Out-Null
        Assert-Equal $timerStartsBeforeRestart $faulted.State.TimerStartCount "$failureKind failure allowed PetLoop restart."
        Invoke-PetLoopTick $faulted 0.099
        Assert-Equal 1 $faulted.State.FaultCount "$failureKind failure emitted Faulted more than once."
    }

    $faultRelease = New-PetLoopFixture $clockType $timerType $hostType $petLoopType
    $faultRelease.Start.Invoke($faultRelease.Loop, @()) | Out-Null
    $faultRelease.State.Pointer = [Dororong.Core.Behavior.PointerSample]::new(
        $true,
        [Dororong.Core.Geometry.PointD]::new(708, 518))
    $faultRelease.State.PrimaryButtonDown = $true
    $faultRelease.Notify.Invoke($faultRelease.Loop, @([Dororong.Core.Geometry.PointD]::new(60, 50))) | Out-Null
    Invoke-PetLoopTick $faultRelease 0.033
    $faultRelease.State.Pointer = [Dororong.Core.Behavior.PointerSample]::new(
        $true,
        [Dororong.Core.Geometry.PointD]::new(713, 518))
    Invoke-PetLoopTick $faultRelease 0.066
    $faultRelease.State.FailInput = $true
    Invoke-PetLoopTick $faultRelease 0.099
    Assert-Equal 1 $faultRelease.State.ReleaseCount 'A fault while DRAGGED did not release capture.'

    $dispose = New-PetLoopFixture $clockType $timerType $hostType $petLoopType
    $dispose.Start.Invoke($dispose.Loop, @()) | Out-Null
    $dispose.State.Pointer = [Dororong.Core.Behavior.PointerSample]::new(
        $true,
        [Dororong.Core.Geometry.PointD]::new(708, 518))
    $dispose.State.PrimaryButtonDown = $true
    $dispose.Notify.Invoke($dispose.Loop, @([Dororong.Core.Geometry.PointD]::new(60, 50))) | Out-Null
    Invoke-PetLoopTick $dispose 0.033
    $dispose.State.Pointer = [Dororong.Core.Behavior.PointerSample]::new(
        $true,
        [Dororong.Core.Geometry.PointD]::new(713, 518))
    Invoke-PetLoopTick $dispose 0.066
    $dispose.State.Trace.Clear()
    $dispose.State.FailTimerStop = $true
    $disposeFailureObserved = $false
    try
    {
        $dispose.Dispose.Invoke($dispose.Loop, @()) | Out-Null
    }
    catch
    {
        $disposeFailureObserved = $true
    }

    Assert-True $disposeFailureObserved 'The injected Dispose cleanup failure did not propagate.'
    Assert-Equal 'timer-stop,timer-detach,clock-stop,release' ($dispose.State.Trace -join ',') 'Dispose stopped cleanup after one injected failure.'
    $cleanupCounts = "$($dispose.State.TimerStopCount),$($dispose.State.TimerDetachCount),$($dispose.State.ClockStopCount),$($dispose.State.ReleaseCount)"
    $dispose.Dispose.Invoke($dispose.Loop, @()) | Out-Null
    Assert-Equal $cleanupCounts "$($dispose.State.TimerStopCount),$($dispose.State.TimerDetachCount),$($dispose.State.ClockStopCount),$($dispose.State.ReleaseCount)" 'A second Dispose repeated cleanup side effects.'
}

$resultDetail = switch ($Focus)
{
    'Startup' { 'startup=create/set/show and fatal cleanup/message/shutdown(1) are one-shot' }
    'PetLoop' { 'actual PetLoop timer/tick/input/brain/window/render/capture/fault/dispose wiring passed' }
    default { 'startup=create/set/show and fatal cleanup/message/shutdown(1) are one-shot; actual PetLoop timer/tick/input/brain/window/render/capture/fault/dispose wiring passed' }
}
Write-Output "RUNTIME COMPOSITION PASS: $resultDetail."
