public enum UIKind
{
    HUD,
    Tooltip,
    SinglePanel,
    PlayerInventory,
    // Coexistence layer (P8 of Docs/JEI管线化-设计草案.md): sits between the
    // panel layer and the player-inventory / tooltip layers, never occupies
    // currentUI, and keeps its own visibility (e.g. JEI follows world panels).
    Overlay
}