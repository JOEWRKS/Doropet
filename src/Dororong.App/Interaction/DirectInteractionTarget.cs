namespace Dororong.App.Interaction;

internal enum DirectInteractionTarget
{
    None,
    Body,
    LeftCheek,
    RightCheek,
    FiveRegionBody
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
