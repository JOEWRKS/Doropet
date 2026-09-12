function Assert-InstallerAcceptanceGatePredicates {
    param(
        [Parameter(Mandatory)]$Handoff,
        [Parameter(Mandatory)][string]$Nonce,
        [Parameter(Mandatory)]$Config,
        [Parameter(Mandatory)][object[]]$Mappings,
        [Parameter(Mandatory)][hashtable]$MappingAvailability,
        [Parameter(Mandatory)][string]$ObservedConfigHash,
        [Parameter(Mandatory)][hashtable]$Identity,
        [Parameter(Mandatory)][bool]$HostOutputAccessible,
        [Parameter(Mandatory)][hashtable]$ActualInputHashes,
        [Parameter(Mandatory)][string]$ReviewedSetupHash,
        [Parameter(Mandatory)][string]$CompilerHash,
        [Parameter(Mandatory)][int]$ResultCount
    )

    if ($Handoff.schemaVersion -ne 1 -or $Nonce -notmatch '^[a-f0-9]{32}$' -or $Nonce -cne $Handoff.nonce) {
        throw 'generated handoff nonce required'
    }
    if ($ObservedConfigHash -cne $Handoff.configSha256) { throw 'configuration hash mismatch' }
    foreach ($setting in @('Networking', 'ClipboardRedirection', 'AudioInput', 'VideoInput', 'PrinterRedirection', 'VGpu')) {
        if ($Config.Configuration.$setting -cne 'Disable') { throw "unsafe $setting configuration" }
    }
    if ($Mappings.Count -ne 4 -or @($Mappings | Where-Object ReadOnly -ceq 'false').Count -ne 1 -or @($Handoff.mappings).Count -ne 4) {
        throw 'mapping count/writable scope mismatch'
    }
    foreach ($mapping in $Handoff.mappings) {
        $match = @($Mappings | Where-Object {
            $_.SandboxFolder -ceq $mapping.guest -and
            $_.HostFolder -ceq $mapping.host -and
            $_.ReadOnly -ceq $mapping.readOnly
        })
        if ($match.Count -ne 1 -or
            -not $MappingAvailability.ContainsKey([string]$mapping.guest) -or
            -not $MappingAvailability[[string]$mapping.guest]) {
            throw 'required generated mapping missing'
        }
    }

    if (-not $Identity.machineGuid -or $Identity.machineGuid -eq $Handoff.hostMachineGuid) {
        throw 'host MachineGuid matches or is unreadable'
    }
    if ($Identity.userSid -eq $Handoff.hostUserSid) { throw 'host user token matches' }
    if ($Identity.computerName -eq $Handoff.hostComputerName -or $Identity.systemUuid -eq $Handoff.hostSystemUuid) {
        throw 'host computer identity matches'
    }
    if ($Identity.identityName -notmatch '\\WDAGUtilityAccount$' -or $Identity.userProfile -cne 'C:\Users\WDAGUtilityAccount') {
        throw 'Sandbox account/profile evidence missing'
    }
    if ($Identity.manufacturer -cne 'Microsoft Corporation' -or $Identity.model -cne 'Virtual Machine' -or -not $Identity.hypervisorPresent) {
        throw 'Microsoft virtual guest evidence missing'
    }
    if ($HostOutputAccessible) { throw 'host output path is directly accessible' }

    foreach ($file in $Handoff.inputFiles) {
        if (-not $ActualInputHashes.ContainsKey([string]$file.name) -or
            $ActualInputHashes[[string]$file.name] -cne $file.sha256) {
            throw "input hash mismatch: $($file.name)"
        }
    }
    if ($ReviewedSetupHash -cne 'AEDE2B7D36A10DEADA800832C99DBD134BC1F4DEEEEA39B48D1F51CE166D5D5D') {
        throw 'reviewed setup hash mismatch'
    }
    if ($CompilerHash -cne 'D06EBD38F38E3CEE60A3C50CC45BD449D77E0BC6A5CABC607EA9886808E4DE1A') {
        throw 'compiler hash mismatch'
    }
    if ($ResultCount -ne 0) { throw 'results mapping must be empty; generate a fresh handoff for each run' }
}
