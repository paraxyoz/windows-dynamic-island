# DESIGN.MD — Windows Dynamic Island (v2.0 Premium Light / Soft Gray Edition)

## 1. Visual Direction & Philosophy
- **Primary Aesthetic:** **Premium Light / Soft Gray** (inspired by modern Apple / macOS desktop hardware-software surfaces with a clean, independent identity).
- **Default Theme:** `AppThemeMode.Light` (`SchemaVersion = 2`).
- **Anti-Slop Principles (`Unslop` + `UI UX Pro Max`):**
  - Zero teal (`#14B8A6`) or acid neon colors anywhere in the interface.
  - Multi-layered soft gray and warm milk-white surfaces for natural depth without sterile flatness.
  - Pure DirectX vector edge rendering (`Effect = null` on `_capsuleBorder`) paired with a separate sibling shadow layer (`_shadowBorder`) and dynamic `RectangleGeometry` inner clipping so corners are 100% smooth and never bleed dark pixels.

## 2. Color Tokens (`ThemeTokens.cs`)

### Default Theme: Premium Light / Soft Gray
| Token | Hex | Purpose |
|---|---|---|
| `CapsuleBackground` | `#F7F7FA` | Collapsed pill & toast surface |
| `PanelBackground` | `#F5F5F7` | Main expanded panel canvas |
| `CardBackground` | `#FFFFFF` | Primary elevated cards and input surfaces |
| `RaisedBackground` | `#EEEEF1` | Secondary surfaces, pills, keycaps, inactive controls |
| `HoverBackground` | `#E4E4E9` | Interactive hover state |
| `PressedBackground` | `#D8D8E0` | Interactive active/pressed state |
| `HairlineBorder` | `#E5E5E9` | Crisp 1px structural borders |
| `SubtleBorder` | `#D4D4DA` | Emphasized / focused borders |
| `PrimaryText` | `#202124` | High-contrast graphite headings and values |
| `SecondaryText` | `#5F6066` | Balanced neutral gray labels |
| `MutedText` | `#85858D` | Captions, placeholders, and hints |
| `Accent` | `#2B2B30` | Tactile Dark Graphite primary action accent |
| `OnAccentText` | `#FFFFFF` | Crisp white text/icons on primary accent |
| `AccentSubtle` | `#E8E8ED` | Soft selection and active badge fill |

### Secondary Theme: Graphite Dark
| Token | Hex | Purpose |
|---|---|---|
| `CapsuleBackground` | `#161618` | Collapsed pill surface |
| `PanelBackground` | `#18181B` | Expanded panel canvas |
| `CardBackground` | `#202024` | Primary card surface |
| `RaisedBackground` | `#29292E` | Secondary control surface |
| `HairlineBorder` | `#2E2E34` | 1px hairline border |
| `PrimaryText` | `#F4F4F6` | Off-white primary typography |
| `SecondaryText` | `#A1A1AA` | Neutral secondary text |
| `MutedText` | `#71717A` | Muted hints |
| `Accent` | `#E4E4E9` | Soft Light Graphite accent |
| `OnAccentText` | `#161619` | Dark text on light accent |

## 3. Calibrated Capsule Geometry & Morphing States
- **Collapsed State:** `246 × 34 px` (Corner Radius `17 px`). Compact horizontal pill housing Animated Smiley `^_^`, Weather pill, Centered Clock, Active Timer badge, and Quick Notes button.
- **Quick Notes Popover State:** `340 × 236 px` (Corner Radius `16 px`). Opens directly when clicking the Notes icon in the collapsed state without expanding the entire island; hovering the Notes button suppresses full-island hover expansion.
- **System Event / Toast State:** `380 × 52 px` (`450 × 52 px` with action buttons, Corner Radius `18 px`).
- **Expanded Control Center State:** `648 × 392 px` (Corner Radius `18 px`).
- **Spring Physics (`AnimationEngine.cs`):** Fixed 2ms (`500 Hz`) substep integration of a critically damped spring (`stiffness = 380`, `damping = 39.0`) with fixed inner layout dimensions on `_expandedLayer` for zero layout thrashing during morph transitions.
