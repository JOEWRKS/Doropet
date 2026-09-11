namespace Dororong.App.Interaction;

internal enum DirectInteractionTarget
{
    None,
    Body,
    LeftCheek,
    RightCheek,
    FiveRegionBody,
    ClickOnly
}

internal enum DirectInteractionPhase
{
    None,
    BodyPending,
    BodyDragEntry,
    BodyDragHold,
    BodyDragSettle,
    CheekPress,
    CheekPull,
    CheekRelease,
    BodyLocalPull,
    BodyLocalSettle
}
