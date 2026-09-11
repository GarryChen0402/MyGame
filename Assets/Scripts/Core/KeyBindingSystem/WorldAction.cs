// World-interaction action resource (P5 of Docs/UI槽位编码与解析映射-实施文档.md):
// movement / jump / attack / use. Carries the legacy binding fields (inherited
// from KeyBinding) under its own table; no response callback - world actions
// are consumed by semantic polling (PlayerInputHandler), unchanged.
public class WorldAction : KeyBinding
{
}
