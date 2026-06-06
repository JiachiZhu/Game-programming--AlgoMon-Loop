using UnityEngine;

public enum CyberUiColorRole
{
    Background,
    RoomPurple,
    Panel,
    PanelBorder,
    Primary,
    Selected,
    Danger,
    Reward,
    Success,
    Disabled,
    TextPrimary,
    TextSecondary
}

public static class CyberUiTheme
{
    public static readonly Color Background = FromRgb(0x05, 0x08, 0x12);
    public static readonly Color RoomPurple = FromRgb(0x7B, 0x2C, 0xFF);
    public static readonly Color Panel = FromRgb(0x07, 0x11, 0x1F);
    public static readonly Color PanelBorder = FromRgb(0x17, 0x4B, 0x74);
    public static readonly Color Primary = FromRgb(0x18, 0xD9, 0xFF);
    public static readonly Color Selected = FromRgb(0x8D, 0xFF, 0xF0);
    public static readonly Color Danger = FromRgb(0xFF, 0x3B, 0x86);
    public static readonly Color Reward = FromRgb(0xFF, 0x9B, 0x35);
    public static readonly Color Success = FromRgb(0x78, 0xF2, 0x8A);
    public static readonly Color Disabled = FromRgb(0x39, 0x41, 0x4F);
    public static readonly Color TextPrimary = FromRgb(0xF3, 0xF7, 0xFF);
    public static readonly Color TextSecondary = FromRgb(0xA8, 0xB6, 0xCE);

    public static Color ColorFor(CyberUiColorRole role)
    {
        switch (role)
        {
            case CyberUiColorRole.Background:
                return Background;
            case CyberUiColorRole.RoomPurple:
                return RoomPurple;
            case CyberUiColorRole.Panel:
                return Panel;
            case CyberUiColorRole.PanelBorder:
                return PanelBorder;
            case CyberUiColorRole.Primary:
                return Primary;
            case CyberUiColorRole.Selected:
                return Selected;
            case CyberUiColorRole.Danger:
                return Danger;
            case CyberUiColorRole.Reward:
                return Reward;
            case CyberUiColorRole.Success:
                return Success;
            case CyberUiColorRole.Disabled:
                return Disabled;
            case CyberUiColorRole.TextSecondary:
                return TextSecondary;
            case CyberUiColorRole.TextPrimary:
            default:
                return TextPrimary;
        }
    }

    public static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }

    public static Color Dim(Color color, float amount)
    {
        return Color.Lerp(color, Background, Mathf.Clamp01(amount));
    }

    private static Color FromRgb(byte r, byte g, byte b)
    {
        return new Color32(r, g, b, 255);
    }
}
