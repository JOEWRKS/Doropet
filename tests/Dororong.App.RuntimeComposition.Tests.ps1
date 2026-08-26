param(
    [string]$Configuration = 'Debug'
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

function New-InternalInstance([Type]$Type)
{
    return [Activator]::CreateInstance(
        $Type,
        [Reflection.BindingFlags]'Instance,Public,NonPublic',
        $null,
        @(),
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

Write-Output 'RUNTIME COMPOSITION PASS: one-shot=1; lifecycle=Created/Running/Faulted/Disposed; cleanup=4/4 with 2 errors preserved; fatal=cleanup/message/shutdown; press=consumed once; capture=entry/exit only.'
