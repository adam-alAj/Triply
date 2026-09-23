---
name: Sunset Wanderer
colors:
  surface: '#faf8ff'
  surface-dim: '#d2d9f4'
  surface-bright: '#faf8ff'
  surface-container-lowest: '#ffffff'
  surface-container-low: '#f2f3ff'
  surface-container: '#eaedff'
  surface-container-high: '#e2e7ff'
  surface-container-highest: '#dae2fd'
  on-surface: '#131b2e'
  on-surface-variant: '#58413d'
  inverse-surface: '#283044'
  inverse-on-surface: '#eef0ff'
  outline: '#8c716c'
  outline-variant: '#e0bfba'
  surface-tint: '#ab3425'
  primary: '#a83223'
  on-primary: '#ffffff'
  primary-container: '#c94a38'
  on-primary-container: '#fffbff'
  inverse-primary: '#ffb4a8'
  secondary: '#49607c'
  on-secondary: '#ffffff'
  secondary-container: '#c7dfff'
  on-secondary-container: '#4b637e'
  tertiary: '#545d63'
  on-tertiary: '#ffffff'
  tertiary-container: '#6d767c'
  on-tertiary-container: '#fbfcff'
  error: '#ba1a1a'
  on-error: '#ffffff'
  error-container: '#ffdad6'
  on-error-container: '#93000a'
  primary-fixed: '#ffdad4'
  primary-fixed-dim: '#ffb4a8'
  on-primary-fixed: '#410100'
  on-primary-fixed-variant: '#8a1c10'
  secondary-fixed: '#d1e4ff'
  secondary-fixed-dim: '#b0c9e8'
  on-secondary-fixed: '#011d35'
  on-secondary-fixed-variant: '#314863'
  tertiary-fixed: '#dbe4eb'
  tertiary-fixed-dim: '#bfc8ce'
  on-tertiary-fixed: '#141d22'
  on-tertiary-fixed-variant: '#3f484e'
  background: '#faf8ff'
  on-background: '#131b2e'
  surface-variant: '#dae2fd'
  surface-canvas: '#F8F9FA'
  surface-card: '#FFFFFF'
  border-subtle: '#E2E8F0'
  text-muted: '#64748B'
  status-success: '#10B981'
  status-warning: '#F59E0B'
  status-error: '#EF4444'
typography:
  display:
    fontFamily: Epilogue
    fontSize: 40px
    fontWeight: '800'
    lineHeight: 48px
    letterSpacing: -0.03em
  display-mobile:
    fontFamily: Epilogue
    fontSize: 32px
    fontWeight: '800'
    lineHeight: 38px
    letterSpacing: -0.02em
  headline-lg:
    fontFamily: Epilogue
    fontSize: 28px
    fontWeight: '700'
    lineHeight: 34px
    letterSpacing: -0.02em
  headline-lg-mobile:
    fontFamily: Epilogue
    fontSize: 24px
    fontWeight: '700'
    lineHeight: 30px
    letterSpacing: -0.01em
  headline-md:
    fontFamily: Epilogue
    fontSize: 20px
    fontWeight: '600'
    lineHeight: 26px
    letterSpacing: -0.01em
  headline-sm:
    fontFamily: Epilogue
    fontSize: 18px
    fontWeight: '600'
    lineHeight: 24px
  body-lg:
    fontFamily: Work Sans
    fontSize: 17px
    fontWeight: '400'
    lineHeight: 26px
  body-md:
    fontFamily: Work Sans
    fontSize: 15px
    fontWeight: '400'
    lineHeight: 22px
  body-sm:
    fontFamily: Work Sans
    fontSize: 13px
    fontWeight: '400'
    lineHeight: 18px
  label-lg:
    fontFamily: Work Sans
    fontSize: 15px
    fontWeight: '600'
    lineHeight: 20px
    letterSpacing: 0.01em
  label-md:
    fontFamily: Work Sans
    fontSize: 13px
    fontWeight: '600'
    lineHeight: 18px
    letterSpacing: 0.02em
  label-sm:
    fontFamily: Work Sans
    fontSize: 11px
    fontWeight: '500'
    lineHeight: 14px
    letterSpacing: 0.04em
rounded:
  sm: 0.5rem
  DEFAULT: 1rem
  md: 1.5rem
  lg: 2rem
  xl: 3rem
  full: 9999px
spacing:
  gutter: 1rem
  gutter-mobile: 0.75rem
  margin: 1.5rem
  margin-mobile: 1rem
  space-xs: 0.25rem
  space-sm: 0.5rem
  space-md: 1rem
  space-lg: 1.5rem
  space-xl: 2rem
---

> **Authoritative visual system.** This document is the source of truth for
> Triply's **visual language** — the color, typography, spacing and radius
> tokens above are canonical, and the Flutter client mirrors them in
> `lib/core/theme/app_colors.dart` and `lib/core/theme/app_text_styles.dart`.
> `08_SYSTEM_DESIGN.md` remains the source of truth for **UX principles,
> layout, accessibility and component architecture**. Where the two documents
> disagree on visual values (for example 08 §4.1's "premium travel-tech"
> wording, or §5.1's exploratory palette), **this document wins** for visuals.
> Do not introduce a second palette.

## Brand & Style

This design system channels an **Experimental Editorial Lifestyle** aesthetic tailored for modern exploration. Blending the tactile elegance of a boutique travel magazine with the deliberate responsiveness of a high-performance mobile application, it treats travel planning not as administrative logistics, but as an evocative narrative.

The emotional signature is adventurous, sophisticated, and lucid. Stripping away cold enterprise utilities and chaotic social media clutter, the design champions expansive composition, vivid focal accents, and structured typography. It serves discerning travelers seeking mindful wanderlust, curated city breaks, and seamless itinerary composition.

Visual language combines the warmth of coastal horizons with crisp informational hierarchy:
- **Expressive Editorial Typography**: Prominent, sculptural headlines create instant visual anchors, paired with reliable, grounded body copy for dense logistics.
- **Vibrant Solar Infusion**: Sun-baked terracotta and coral punctuate calming oceanic depths, steering visual momentum across the viewport.
- **Fluid Architectural Structure**: Soft, pill-contoured components, generous breathing margins, and tactile touch zones optimized for thumb-driven mobile navigation.

## Colors

The color palette centers on a sun-warmed narrative anchored by architectural neutrals. The hex values below are the **authoritative tokens** (the front-matter `colors:`) and match the Flutter theme:

- **Primary (`#A83223`)**: Sunset Terracotta acts as the dynamic catalyst. It powers high-priority interactive touchpoints, hero badges, active navigation indicators, and visual highlights that denote wanderlust and emotional investment. (`on-primary` `#FFFFFF`; container `#C94A38` on `#FFFBFF`.)
- **Secondary (`#49607C`)**: Deep Slate Blue provides structure, grounding interactive chrome, deep section headers, and high-contrast structural framing.
- **Tertiary (`#DBE4EB`)**: Soft Blue Tint provides subtle contrast for active item backings, quiet callout fills, the AI-generated badge, and soft categorical grouping.
- **Neutral (`#131B2E`)**: Deep Navy handles text hierarchy with unmatched clarity, offering softened high-contrast readability against light backgrounds. (`text-muted` `#64748B`, `on-surface-variant` `#58413D`.)

### Functional Roles & Tone
- Backgrounds leverage the app `surface` (`#FAF8FF`) to reduce eye strain, while content layers sit atop pure `surface-card` / `surface-container-lowest` (`#FFFFFF`). `surface-canvas` (`#F8F9FA`) remains an optional utility canvas.
- Informational states employ semantic tokens: `status-success` (`#10B981`) for confirmed itineraries and saved bookings, `status-warning` (`#F59E0B`) for weather alerts and tight transfers, and `status-error` (`#EF4444`) for booking conflicts and destructive flows.
- Borders rely on `border-subtle` (`#E2E8F0`) to provide quiet spatial boundaries without cluttering views.

## Typography

The typographic hierarchy juxtaposes the editorial personality of **Epilogue** with the utilitarian clarity of **Work Sans**:

- **Headlines & Display (Epilogue)**: Used across destination hero titles, wizard step headers, and card titles. Epilogue’s geometric yet expressive letterforms inject character and lifestyle cachet, transforming standard itineraries into dynamic storyboards.
- **Body & Functional Chrome (Work Sans)**: Used for travel descriptions, time stamps, tabular budgets, and interface controls. Work Sans maintains optimal legibility across varying viewports and lighting conditions.

### Editorial Guidelines
- Use sentence casing for all headings, labels, and buttons. Avoid uppercase styling except for micro status tokens or currency identifiers (e.g., `USD`, `EUR`).
- Financial metrics, departure schedules, and day counters must utilize tabular or proportional numbers to maintain alignment across list stacks.
- Display levels larger than `28px` must switch to their corresponding `-mobile` tokens on viewports under 600px width.

## Layout & Spacing

The layout is built around a **mobile-first 8-point base rhythm**, scaling smoothly into multi-column editorial modules on wider viewports:

- **Vertical Cadence**: All spatial offsets derive from 8px (`0.5rem`) increments, with 4px (`0.25rem`) reserved for tight inline pairings (such as icon-to-label alignment).
- **Mobile Hand Zones**: Primary actions, day selectors, and filter ribbons reside within bottom thumb reaches. Pinned screen bars enforce `space-lg` (`1.5rem`) clearance above hardware home indicators.
- **Grid Adaptability**:
  - *Mobile (< 600px)*: Single vertical flow utilizing `margin-mobile` (`1rem`) and `gutter-mobile` (`0.75rem`), with horizontal edge-to-edge carousels snapping to internal margins.
  - *Tablet & Foldables (600px - 1024px)*: Two-column split-pane (itinerary timeline juxtaposed with interactive place cards) with `margin` (`1.5rem`) and `gutter` (`1rem`).
  - *Desktop / Web (> 1024px)*: Constrained container max-width at 1200px, centering travel modules with generous structural negative space.

## Elevation & Depth

Visual depth is achieved through layered tonal contrast rather than opaque skeuomorphic styling, reflecting a sunlit architectural palette:

- **Level 0 (Flat Canvas)**: Global screen backgrounds rest on `surface-canvas` (`#F8F9FA`). No shadows are applied.
- **Level 1 (Interactive Cards & Tiles)**: Resting cards utilize pure `surface-card` (`#FFFFFF`) with a subtle 1px hairline border (`#E2E8F0`) and an ambient tinted shadow:
  `box-shadow: 0 4px 20px -2px rgba(15, 41, 66, 0.05)`.
- **Level 2 (Floating Controls & Popovers)**: Pill navigation bars and floating trip filters use deeper dispersion with warm undertones:
  `box-shadow: 0 8px 24px -4px rgba(168, 50, 35, 0.08), 0 4px 12px -2px rgba(15, 25, 42, 0.04)`.
- **Level 3 (Modal Sheets & Drawer Overlays)**: Bottom sheets use top-only rounded edges and an anchoring shadow:
  `box-shadow: 0 -8px 32px 0 rgba(15, 23, 42, 0.12)`.
- **Image Scrims**: Photographic hero tiles apply a directional linear gradient from transparent to `rgba(15, 25, 42, 0.72)` at the bottom, maintaining high contrast for superimposed labels.

## Shapes

The design system adopts a **pill-shaped (level 3)** curvature language, echoing the soft edges of river stones, coastal pebbles, and contemporary industrial design:

- **Controls & Micro-surfaces (`1rem` / 16px to full pill)**: Form inputs, action buttons, category tags, and search bars use generous pill radii, creating inviting touch targets.
- **Standard Cards (`rounded-lg` - 2rem / 32px)**: Destination cards, itinerary day modules, and booking confirmation blocks utilize wide rounded curves to feel distinct and approachable.
- **Sheets & Modals (`rounded-xl` - 3rem / 48px top radius)**: Bottom detail sheets, day planner overlays, and route drawers use exaggerated top corners that soften transitions when pulled into view.

## Components

### Buttons
- **Primary Action**: Full pill radius, filled with `primary` (`#A83223`), label in white `Work Sans` (`label-lg`). Includes a subtle glow shadow on press.
- **Secondary Action**: Pill radius, background `tertiary` (`#E8F1F8`), label in `secondary` (`#0F2942`). Zero border.
- **Ghost / Tertiary Action**: Borderless with `secondary` or `text-muted` typography, used for low-priority navigation and header dismissals.

### Input Fields
- Enclosed pill contours with `surface-card` (`#FFFFFF`) background, bordered by `border-subtle` (`#E2E8F0`).
- Label rests outside in `label-sm` (`#64748B`). Active focus transitions the border to `primary` (`#A83223`) with a 2px soft ring. Error states swap the border to `status-error` (`#EF4444`).

### Chips & Badges
- **Selection Chips**: Pill silhouette. Inactive chips use a 1px border (`#E2E8F0`) with `text-muted` typography. Selected state transitions to full `secondary` (`#49607C`) or `primary` (`#A83223`) with high-contrast white text.
- **AI Recommendation Badges**: Subtle pill tags with an icon, styled in `tertiary` (`#DBE4EB`) background with `secondary` (`#49607C`) text, avoiding artificial neon styling.
- **Data Provenance Badges**: one badge per provenance type, each visually and verbally distinct so AI content and estimated numbers can never be confused (Principle 05 / SRS §8). `AIGeneratedBadge` ("AI-generated", cool `#DBE4EB` + sparkle icon) marks AI-authored **content**; `EstimatedBadge` ("Estimated", warm `#FFDAD4`) marks cost **estimates**; `VerifiedBadge` ("Verified", semantic `#10B981` + check icon) marks values checked against real data.

### Content Cards
- **Trip / Destination Card**: Constructed with `rounded-lg` curvature, featuring an edge-to-edge photography window over a clean lower metadata panel (`#FFFFFF`). Displays a title (`headline-md`), location pin, and a floating price/duration pill badge.
- **Itinerary Timeline Card**: Bordered by `border-subtle`, displaying time offsets in tabular typography, an accent dot in `primary` (`#A83223`), and expandable activity descriptions.

### Lists & Selection Rows
- Item groups are separated by generous spacing or ultra-thin hairline dividers (`#E2E8F0`).
- Checkboxes and radio switches use circular pill geometries, filling with `primary` (`#A83223`) upon activation.

### Bottom Sheets & Overlays
- Fixed to viewport bottom with `rounded-xl` top curvature. Includes a centered drag indicator handle (36px × 4px, `#E2E8F0`) with `space-sm` clearance from the top.