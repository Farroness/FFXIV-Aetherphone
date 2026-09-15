using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Muster;
using Aetherphone.Core.PartyFinder;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Aetherphone.Core.Social;

namespace Aetherphone.Apps.Muster;

internal static class MusterCard
{
    private const float PadY = 13f;
    private const float AvatarRadius = 19f;
    private const float IdentityRowHeight = 50f;
    private const float MetaRowHeight = 28f;
    private const float DescriptionGap = 9f;
    private const float BadgeGap = 7f;
    private const int MaxDescriptionLines = 2;

    public static readonly Vector4 LiveGreen = new(0.24f, 0.82f, 0.44f, 1f);
    private static readonly Vector4 CapacityAmber = new(0.98f, 0.72f, 0.30f, 1f);

    public static float Height(in MusterEntry entry, float width, float scale)
    {
        var lines = DescriptionLines(entry.Description, InnerWidth(width, scale), out var lineHeight);
        var descriptionHeight = lines > 0 ? lines * lineHeight + DescriptionGap * scale : 0f;
        return (PadY * 2f + IdentityRowHeight + MetaRowHeight) * scale + descriptionHeight;
    }

    public static bool Draw(in FeedCellScope cell, in MusterEntry entry, RemoteImageCache images,
        LodestoneService lodestone, PhoneTheme theme, AppSkin ui, long nowUnix, int currentDataCenterId)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var palette = ui.Palette;
        var card = cell.Bounds;
        var live = entry.StartsAtUnix <= nowUnix;

        var pad = FeedCell.PadX * scale;
        var left = card.Min.X + pad;
        var right = card.Max.X - pad;
        var rowTop = card.Min.Y + PadY * scale;
        var avatarRadius = AvatarRadius * scale;
        var avatarCenter = new Vector2(left + avatarRadius, rowTop + IdentityRowHeight * scale * 0.5f);
        if (entry.TryGetMuster(out var muster))
        {
            AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme, MusterText.HostLabel(muster),
                muster.HostWorld, null, images, lodestone, 1.05f, 32, 1f, Frames.Of(muster.HostFrameId));
        }
        else
        {
            IconTile.Draw(avatarCenter, avatarRadius * 2f, IconTile.Surface(palette.Accent), FontAwesomeIcon.Users);
        }

        drawList.AddCircle(avatarCenter, avatarRadius + 1.5f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(palette.Accent, live ? 0.45f : 0.22f)), 32, 1.4f * scale);

        var status = live
            ? Loc.T(L.Common.Live)
            : Loc.T(L.Muster.StartsIn, MusterText.Span(entry.StartsAtUnix - nowUnix));
        var pillLeft = DrawStatusPill(drawList, right, rowTop + 13f * scale, live, status,
            live ? LiveGreen : palette.Accent, scale);

        var textLeft = avatarCenter.X + avatarRadius + 12f * scale;
        var name = Typography.FitText(entry.Identity, pillLeft - 10f * scale - textLeft, TextStyles.Title3);
        Typography.Draw(drawList, new Vector2(textLeft, rowTop + 2f * scale), name, palette.TitleInk,
            TextStyles.Title3);

        var chipCenterY = rowTop + 36f * scale;
        var chipRight = right;
        if (currentDataCenterId != 0 && entry.DataCenterId != 0 && entry.DataCenterId != currentDataCenterId)
        {
            chipRight = DrawDataCenterChip(drawList, right, chipCenterY, palette.Accent, scale) - 8f * scale;
        }

        var chipIcon = entry.IsMuster
            ? MusterCategories.Icon(entry.Category)
            : PartyFinderDutyKinds.Icon(entry.PartyFinderDutyKind);
        var chipLabel = entry.IsMuster
            ? Loc.T(MusterCategories.Label(entry.Category))
            : Loc.T(PartyFinderDutyKinds.Label(entry.PartyFinderDutyKind));
        DrawCategoryChip(drawList, textLeft, chipCenterY, chipIcon, chipLabel, chipRight - textLeft, palette.Accent,
            scale);

        var descriptionTop = rowTop + IdentityRowHeight * scale;
        DrawDescription(drawList, entry.Description, left, descriptionTop, right - left, palette.BodyInk);

        var metaTop = card.Max.Y - PadY * scale - MetaRowHeight * scale;
        DrawMetaRow(drawList, entry, left, right, metaTop + MetaRowHeight * scale * 0.5f, palette, scale);

        return cell.Tapped;
    }

    private static float InnerWidth(float width, float scale) => width - FeedCell.PadX * 2f * scale;

    private static float DrawStatusPill(ImDrawListPtr drawList, float right, float centerY, bool live, string label,
        Vector4 tint, float scale)
    {
        var textSize = Typography.Measure(label, TextStyles.FootnoteEmphasized);
        var height = 22f * scale;
        var dotSpace = live ? 15f * scale : 0f;
        var width = textSize.X + dotSpace + 20f * scale;
        var min = new Vector2(right - width, centerY - height * 0.5f);
        var max = new Vector2(right, centerY + height * 0.5f);
        var breath = live ? 0.07f * Pulse.Wave(Pulse.Calm) : 0f;
        Squircle.Fill(drawList, min, max, height * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(tint, 0.15f + breath)));
        Squircle.Stroke(drawList, min, max, height * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(tint, 0.34f + breath)), 1f * scale);
        if (live)
        {
            DrawLiveDot(drawList, new Vector2(min.X + 13f * scale, centerY), scale);
        }

        Typography.Draw(drawList, new Vector2(min.X + 10f * scale + dotSpace, centerY - textSize.Y * 0.5f), label,
            tint, TextStyles.FootnoteEmphasized);
        return min.X;
    }

    public static void DrawLiveDot(ImDrawListPtr drawList, Vector2 center, float scale)
    {
        var phase = Pulse.Phase(Pulse.Calm);
        DrawLiveRing(drawList, center, phase, scale);
        DrawLiveRing(drawList, center, phase < 0.5f ? phase + 0.5f : phase - 0.5f, scale);
        var core = 3f + 0.5f * Pulse.Wave(Pulse.Calm);
        drawList.AddCircleFilled(center, core * scale, ImGui.GetColorU32(LiveGreen), 20);
    }

    private static void DrawLiveRing(ImDrawListPtr drawList, Vector2 center, float phase, float scale)
    {
        var radius = (3.4f + 6f * phase) * scale;
        drawList.AddCircle(center, radius, ImGui.GetColorU32(Palette.WithAlpha(LiveGreen, 0.55f * (1f - phase))),
            20, 1.4f * scale);
    }

    private static void DrawCategoryChip(ImDrawListPtr drawList, float left, float centerY, FontAwesomeIcon icon,
        string label, float maxWidth, Vector4 accent, float scale)
    {
        var height = 22f * scale;
        var iconSpace = 16f * scale;
        var fitted = Typography.FitText(label, maxWidth - iconSpace - 22f * scale,
            TextStyles.SubheadlineEmphasized);
        var textSize = Typography.Measure(fitted, TextStyles.SubheadlineEmphasized);
        var min = new Vector2(left, centerY - height * 0.5f);
        var max = new Vector2(left + textSize.X + iconSpace + 22f * scale, centerY + height * 0.5f);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.16f)));
        AppSkin.Icon(drawList, new Vector2(min.X + 13f * scale, centerY), IconGlyph.Of(icon), accent, 0.6f);
        Typography.Draw(drawList, new Vector2(min.X + 11f * scale + iconSpace, centerY - textSize.Y * 0.5f), fitted,
            accent, TextStyles.SubheadlineEmphasized);
    }

    private static float DrawSourceBadge(ImDrawListPtr drawList, float left, float centerY, bool isPartyFinder,
        in AppPalette palette, float scale)
    {
        var label = isPartyFinder ? Loc.T(L.Muster.PartyFinderBadge) : Loc.T(L.Muster.MusterBadge);
        var tint = isPartyFinder ? palette.Accent : Palette.WithAlpha(palette.MutedInk, 0.9f);
        var fillAlpha = isPartyFinder ? 0.16f : 0.08f;
        var icon = isPartyFinder ? FontAwesomeIcon.Search : FontAwesomeIcon.Bullhorn;
        var textSize = Typography.Measure(label, TextStyles.Caption1);
        var chipHeight = 20f * scale;
        var iconSpace = 14f * scale;
        var min = new Vector2(left, centerY - chipHeight * 0.5f);
        var max = new Vector2(left + textSize.X + iconSpace + 18f * scale, centerY + chipHeight * 0.5f);
        Squircle.Fill(drawList, min, max, chipHeight * 0.5f, ImGui.GetColorU32(Palette.WithAlpha(tint, fillAlpha)));
        AppSkin.Icon(drawList, new Vector2(min.X + 11f * scale, centerY), IconGlyph.Of(icon), tint, 0.52f);
        Typography.Draw(drawList, new Vector2(min.X + 9f * scale + iconSpace, centerY - textSize.Y * 0.5f), label,
            tint, TextStyles.Caption1);
        return max.X;
    }

    private static float DrawDataCenterChip(ImDrawListPtr drawList, float right, float centerY, Vector4 accent,
        float scale)
    {
        var label = Loc.T(L.Muster.DcTravel);
        var textSize = Typography.Measure(label, TextStyles.Caption1);
        var chipHeight = 20f * scale;
        var iconSpace = 14f * scale;
        var min = new Vector2(right - textSize.X - iconSpace - 18f * scale, centerY - chipHeight * 0.5f);
        var max = new Vector2(right, centerY + chipHeight * 0.5f);
        Squircle.Fill(drawList, min, max, chipHeight * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(accent, 0.14f)));
        AppSkin.Icon(drawList, new Vector2(min.X + 11f * scale, centerY), IconGlyph.Of(FontAwesomeIcon.Plane),
            Palette.WithAlpha(accent, 0.9f), 0.52f);
        Typography.Draw(drawList, new Vector2(min.X + 9f * scale + iconSpace, centerY - textSize.Y * 0.5f), label,
            Palette.WithAlpha(accent, 0.9f), TextStyles.Caption1);
        return min.X;
    }

    private static void DrawDescription(ImDrawListPtr drawList, string description, float left, float top,
        float textWidth, Vector4 ink)
    {
        if (description.Length == 0)
        {
            return;
        }

        using (Plugin.Fonts.Push(TextStyles.Callout.Scale, TextStyles.Callout.Weight))
        {
            Plugin.Fonts.NoticeText(description);
            var lines = Typography.WrapCurrent(description, textWidth);
            var count = Math.Min(lines.Length, MaxDescriptionLines);
            var lineHeight = ImGui.GetTextLineHeightWithSpacing();
            var font = ImGui.GetFont();
            var fontSize = ImGui.GetFontSize();
            var packed = ImGui.GetColorU32(ink);
            for (var index = 0; index < count; index++)
            {
                drawList.AddText(font, fontSize, new Vector2(left, top + index * lineHeight), packed, lines[index]);
            }
        }
    }

    private static void DrawMetaRow(ImDrawListPtr drawList, in MusterEntry entry, float left, float right,
        float centerY, in AppPalette palette, float scale)
    {
        var going = entry.IsMuster
            ? Loc.T(L.Muster.GoingCount, entry.SlotsFilled)
            : Loc.T(L.Muster.PartySlots, entry.SlotsFilled, entry.SlotsTotal);
        var goingSize = Typography.Measure(going, TextStyles.SubheadlineEmphasized);
        var goingLeft = right - goingSize.X;
        Typography.Draw(drawList, new Vector2(goingLeft, centerY - goingSize.Y * 0.5f), going,
            entry.SlotsFilled > 0 ? palette.TitleInk : palette.MutedInk, TextStyles.SubheadlineEmphasized);
        AppSkin.Icon(drawList, new Vector2(goingLeft - 11f * scale, centerY), IconGlyph.Of(FontAwesomeIcon.Users),
            Palette.WithAlpha(palette.MutedInk, 0.85f), 0.58f);
        var metaRight = goingLeft - 21f * scale;
        if (entry.SlotsTotal > 0 && entry.SlotsFilled >= entry.SlotsTotal)
        {
            var capacity = Loc.T(L.Muster.AtCapacity);
            var capacitySize = Typography.Measure(capacity, TextStyles.FootnoteEmphasized);
            metaRight -= capacitySize.X + 10f * scale;
            Typography.Draw(drawList, new Vector2(metaRight + 10f * scale, centerY - capacitySize.Y * 0.5f),
                capacity, CapacityAmber, TextStyles.FootnoteEmphasized);
        }

        var badgeRight = DrawSourceBadge(drawList, left, centerY, !entry.IsMuster, palette, scale);
        var place = entry.Place;
        if (place.Length == 0)
        {
            return;
        }

        var placeIconLeft = badgeRight + BadgeGap * scale;
        var placeLeft = placeIconLeft + 15f * scale;
        var placeWidth = metaRight - placeLeft;
        if (placeWidth <= 0f)
        {
            return;
        }

        AppSkin.Icon(drawList, new Vector2(placeIconLeft + 5f * scale, centerY),
            IconGlyph.Of(FontAwesomeIcon.MapMarkerAlt), Palette.WithAlpha(palette.MutedInk, 0.85f), 0.58f);
        var placeSize = Typography.Measure(place, TextStyles.Subheadline);
        Marquee.DrawLeftAuto(drawList, new MarqueeId("muster.card.place.", entry.CacheKey), place, placeLeft,
            centerY - placeSize.Y * 0.5f, placeWidth, TextStyles.Subheadline, palette.MutedInk);
    }

    private static int DescriptionLines(string description, float textWidth, out float lineHeight)
    {
        using (Plugin.Fonts.Push(TextStyles.Callout.Scale, TextStyles.Callout.Weight))
        {
            lineHeight = ImGui.GetTextLineHeightWithSpacing();
            if (description.Length == 0)
            {
                return 0;
            }

            Plugin.Fonts.NoticeText(description);
            var lines = Typography.WrapCurrent(description, textWidth);
            return Math.Min(lines.Length, MaxDescriptionLines);
        }
    }
}
