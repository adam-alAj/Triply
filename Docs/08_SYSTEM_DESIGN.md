# TRIPLY --- MOBILE DESIGN SYSTEM & UI/UX DIRECTION

**Document:** `DESIGN.md`\
**Product:** Triply --- AI-Powered Travel Planner\
**Platform:** Mobile-first Flutter application\
**Design Tool:** Stitch AI\
**Design Status:** Superseded for visual design by `09_DESIGN.md`; retained as UX principles and interaction reference\
**Audience:** UI/UX, Graphic Design, Flutter, Frontend, AI/ML, Backend
teams

------------------------------------------------------------------------

## 0. Authority & Relationship to `09_DESIGN.md`

This document owns Triply's **UX philosophy, principles, layout,
accessibility and component architecture** — the *why* and *how the product
should behave*.

`09_DESIGN.md` ("Sunset Wanderer") owns the **concrete visual tokens** —
colors, typography, spacing and radii — and is the **authoritative visual
system**; the Flutter theme (`lib/core/theme/app_colors.dart`,
`lib/core/theme/app_text_styles.dart`) implements its tokens directly.

Where the two documents disagree on visual values, `09_DESIGN.md` wins. In
particular, the "Suggested conceptual palette" in §5.1 and the "premium
travel-tech" wording in §4.1 predate the token system and are **superseded**
as literal visual direction by `09_DESIGN.md`; they remain valid as *intent*
(clean, spacious, image-led, restrained, structured). This closes the
08-vs-09 design conflict: **09 is authoritative for visuals, 08 for
principles and interaction.**

The screen inventory and implemented flow are maintained in `07_UI_PAGES.md`.
Its combined Budget & Destination Selection step supersedes the separate
Budget Input and Destination Selection screens described in this earlier
design draft.

------------------------------------------------------------------------

## 1. Purpose

This document defines the visual and UX direction for Triply before
screen-by-screen design begins in Stitch AI.

It converts the approved mobile information architecture into a coherent
design language.

The purpose is not to describe individual screens in isolation. The
purpose is to ensure that every Triply screen feels like one product,
follows the same interaction principles, and can realistically be
implemented in Flutter.

The approved mobile specification defines Triply around one core loop:

**PLAN → GENERATE → VIEW → CUSTOMIZE → SAVE**

The design must make this loop feel effortless.

This earlier screen list is retained as design history; its separate budget
and destination steps are superseded by the merged flow documented in
`07_UI_PAGES.md`. Use that document for the current screen inventory and
P0 count. This draft listed:

-   Login
-   Register
-   Home
-   Planning Mode
-   Budget Input
-   Destination Selection
-   Destination Suggestions
-   Trip Details
-   Interests
-   Review & Confirm
-   Generating
-   Trip Overview
-   My Trips

The specification also defines supporting non-page surfaces:

-   Place / Item Detail --- Bottom Sheet
-   Edit Itinerary Item --- Modal
-   Regenerate Options --- Bottom Sheet
-   Archive / Delete Confirmation --- Dialog

The exact information architecture remains authoritative. This document
controls the visual and interaction language, not product scope.

------------------------------------------------------------------------

# 2. Product Design Philosophy

## 2.1 Core Design Statement

> **Triply turns complicated travel planning into a calm, guided,
> personalized experience.**

Triply should feel like a smart travel companion, not a complicated
planning dashboard.

The interface should answer three questions at every moment:

1.  **Where am I?**
2.  **What should I do next?**
3.  **What will happen when I do it?**

If the user has to think about the interface instead of the trip, the
design has failed.

------------------------------------------------------------------------

## 2.2 Personality

Triply should be:

-   Intelligent
-   Warm
-   Trustworthy
-   Exploratory
-   Calm
-   Premium
-   Human
-   Practical
-   Modern

Triply should NOT feel:

-   Corporate and sterile
-   Childish
-   Gamified
-   Overly futuristic
-   Technically intimidating
-   Like an AI developer tool
-   Like a financial dashboard
-   Like a social-media feed

------------------------------------------------------------------------

# 3. Design Principles

## Principle 01 --- One Primary Action

Every screen must have one visually dominant action.

Examples:

-   Login → **Log In**
-   Register → **Create Account**
-   Planning Mode → **Continue**
-   Interests → **Continue**
-   Review → **Generate Trip**
-   Generating → wait / retry
-   Trip Overview → **Save**

Secondary actions must visually remain secondary.

------------------------------------------------------------------------

## Principle 02 --- Progressive Disclosure

Do not show every piece of information at once.

Show the minimum information required for the current decision.

Complex information should appear progressively through:

-   Sections
-   Cards
-   Bottom sheets
-   Modals
-   Tabs
-   Expandable content

This is particularly important for itinerary and cost information.

------------------------------------------------------------------------

## Principle 03 --- Confidence Before Commitment

Before an expensive or irreversible action, the interface must give the
user confidence.

The strongest example is:

**Review & Confirm → Generate Trip**

The Review screen should make the user feel:

> "I know exactly what Triply is going to use to build my trip."

------------------------------------------------------------------------

## Principle 04 --- AI Should Feel Helpful, Not Magical

AI should be visible enough to communicate intelligence but never become
visual noise.

Use subtle language such as:

-   AI-generated
-   Personalized for you
-   Estimated
-   Suggested based on your preferences

Never use fake technical explanations.

Do not display invented confidence percentages, model scores, processing
metrics, or fake AI animations.

------------------------------------------------------------------------

## Principle 05 --- Estimated Means Estimated

Triply's generated costs are estimates.

The visual system must clearly distinguish:

**AI-generated / Estimated**

from verified facts.

Estimated prices should never visually resemble guaranteed prices.

------------------------------------------------------------------------

## Principle 06 --- No Dead Ends

Errors and empty states should always answer:

-   What happened?
-   Why does it matter?
-   What can I do next?

Example:

Instead of:

> No results.

Use:

> We couldn't find destinations that fit this budget yet.

Then provide a useful next action such as:

**Adjust Budget**

or

**Explore Supported Destinations**

------------------------------------------------------------------------

## Principle 07 --- Design for the Thumb

Primary controls should be reachable and comfortable on a phone.

Prefer:

-   Bottom actions
-   Large touch targets
-   Thumb-friendly controls
-   Short interaction paths

Avoid placing important actions exclusively in difficult-to-reach top
corners.

------------------------------------------------------------------------

## Principle 08 --- Consistency Beats Novelty

A familiar interaction used consistently is better than a clever
interaction used once.

Reuse:

-   Same button behavior
-   Same card behavior
-   Same spacing
-   Same typography
-   Same navigation patterns
-   Same feedback patterns

------------------------------------------------------------------------

# 4. Visual Direction

## 4.1 Overall Aesthetic

Triply should use a **premium travel-tech aesthetic**.

Visual characteristics:

-   Clean
-   Spacious
-   Image-led where useful
-   Soft but structured
-   Strong typography
-   Moderate rounding
-   Subtle elevation
-   Refined iconography
-   Restrained color accents

The interface should have enough visual personality to be memorable
without becoming decorative.

------------------------------------------------------------------------

## 4.2 Visual Hierarchy

Use hierarchy in this order:

1.  Screen purpose
2.  Primary content / decision
3.  Primary CTA
4.  Supporting information
5.  Secondary actions
6.  Metadata

The user should be able to scan a screen in 2--3 seconds and understand
its purpose.

------------------------------------------------------------------------

# 5. Color System

## 5.1 Recommended Brand Direction

> Superseded by `09_DESIGN.md` for literal values — the palette below records
> the original exploration. The shipped palette anchors on a terracotta
> primary with a deep slate-blue secondary.

Use a **deep travel blue as the primary brand anchor**, supported by a
clean off-white background and a warm exploration accent.

Suggested conceptual palette:

  Token            Direction                    Purpose
  ---------------- ---------------------------- -----------------------------
  Primary          Deep Ocean Blue              Brand, primary CTA
  Primary Soft     Light Blue Tint              Selected backgrounds
  Accent           Warm Coral / Sunset Orange   Exploration and emphasis
  Background       Warm Off-White               Main app background
  Surface          White                        Cards and elevated surfaces
  Text Primary     Deep Navy                    Main text
  Text Secondary   Muted Slate                  Supporting text
  Border           Soft Gray-Blue               Dividers
  Success          Natural Green                Success states
  Warning          Warm Amber                   Warnings
  Error            Clear Red                    Errors
  Info             Calm Blue                    Informational states

Do not use every color on every screen.

The majority of the interface should remain neutral.

------------------------------------------------------------------------

## 5.2 Color Rules

### Primary Color

Use for:

-   Main CTA
-   Selected navigation
-   Important active states
-   Key interactive controls

Do not use the primary color for large decorative backgrounds
everywhere.

### Accent

Use sparingly for:

-   Exploration cues
-   Special highlights
-   Important but non-primary emphasis

### Semantic Colors

Semantic colors must have consistent meaning.

Never use red merely as decoration.

Never use green merely because it looks attractive.

------------------------------------------------------------------------

# 6. Typography

## 6.1 Typography Philosophy

Typography is one of the primary tools for making Triply feel premium.

Prioritize:

-   Excellent readability
-   Strong hierarchy
-   Comfortable line height
-   Clear distinction between headings and metadata

Use one primary UI font family unless multilingual requirements require
a compatible secondary font.

------------------------------------------------------------------------

## 6.2 Type Scale

Recommended conceptual scale:

  Style        Use
  ------------ --------------------------------
  Display      Hero / major destination title
  H1           Screen title
  H2           Major section
  H3           Card / subsection
  Body Large   Important explanatory text
  Body         Default text
  Body Small   Supporting information
  Label        Inputs / categories
  Caption      Metadata
  Button       CTA labels

Avoid excessive font-size variation.

------------------------------------------------------------------------

## 6.3 Typography Rules

-   Use sentence case.
-   Avoid unnecessary ALL CAPS.
-   Keep headings concise.
-   Never use tiny text for critical information.
-   Make monetary values easy to scan.
-   Make dates and trip duration visually distinct.
-   Use tabular/consistent numeral treatment for costs where supported.

------------------------------------------------------------------------

# 7. Spacing System

Use an 8-point spacing rhythm.

Recommended base values:

-   4 --- micro spacing
-   8 --- tight spacing
-   12 --- compact spacing
-   16 --- standard spacing
-   24 --- section spacing
-   32 --- major spacing
-   40 --- large separation
-   48+ --- hero/major composition

Avoid arbitrary spacing values unless required by the platform.

------------------------------------------------------------------------

# 8. Shape Language

## 8.1 Corner Radius

Use a restrained radius system.

Recommended:

-   Small controls: 8--10
-   Inputs/cards: 12--16
-   Major cards: 16--20
-   Bottom sheets: 24+ at top corners
-   Full-screen containers: platform-native behavior

Do not make every element extremely rounded.

Triply should feel polished, not toy-like.

------------------------------------------------------------------------

## 8.2 Elevation

Use subtle elevation.

Cards should separate from the background primarily through:

1.  Spacing
2.  Surface contrast
3.  Very subtle shadow

Avoid heavy shadows.

------------------------------------------------------------------------

# 9. Iconography

Use one consistent icon family.

Icons should be:

-   Simple
-   Modern
-   Geometric
-   Recognizable
-   Consistent in stroke weight

Avoid mixing:

-   Filled icons
-   Thin outline icons
-   3D icons
-   Illustrative icons

unless the distinction is intentional and documented.

------------------------------------------------------------------------

# 10. Photography & Imagery

Travel imagery is one of Triply's strongest emotional tools.

Use destination photography selectively.

## Image Principles

Images should be:

-   High quality
-   Authentic
-   Aspirational
-   Relevant to the destination
-   Natural
-   Bright enough to support readable overlays

Avoid generic stock images whenever a meaningful destination image can
be used.

Do not overload the application with images.

Use imagery primarily for:

-   Destination cards
-   Destination suggestions
-   Trip header
-   Empty/onboarding moments where useful

The itinerary itself should remain information-first.

------------------------------------------------------------------------

# 11. Cards

Cards are important in Triply but must not become the default container
for everything.

## Destination Card

Should communicate:

-   Image
-   Destination name
-   One useful supporting attribute
-   Selection state when applicable

## Trip Card

Should communicate:

-   Destination
-   Dates
-   Status
-   Estimated cost when useful
-   Clear tap affordance

## Itinerary Card

This is one of the most important components.

Information hierarchy:

**TIME → ACTIVITY / PLACE → SUPPORTING INFO → ESTIMATED COST**

The card must be scannable without reading paragraphs.

------------------------------------------------------------------------

# 12. Buttons

## Primary Button

Use for the main action.

Characteristics:

-   High contrast
-   Strong label
-   Comfortable height
-   Full-width when appropriate in wizard screens
-   Fixed bottom placement when it improves usability

Examples:

-   Continue
-   Generate Trip
-   Save Trip
-   Retry

------------------------------------------------------------------------

## Secondary Button

Use for:

-   Alternative action
-   Edit
-   Back to Review
-   Explore another option

Must not compete visually with the primary action.

------------------------------------------------------------------------

## Destructive Button

Use only for destructive actions.

Examples:

-   Delete
-   Remove

Always pair destructive actions with clear confirmation when
appropriate.

------------------------------------------------------------------------

# 13. Inputs

Inputs must feel calm and simple.

Each input should have:

-   Clear label
-   Useful placeholder where necessary
-   Focus state
-   Error state
-   Disabled state
-   Validation feedback

Do not rely exclusively on placeholder text as the label.

------------------------------------------------------------------------

# 14. Wizard UX

The Trip Creation Wizard is the most important interaction sequence.

It should feel like a conversation with a knowledgeable travel planner
rather than a long form.

## Wizard Structure

Every step should generally contain:

**Top** - Back - Progress indicator

**Middle** - Question / title - Short explanation - Input or selection

**Bottom** - Primary CTA - Optional secondary action

------------------------------------------------------------------------

## Progress

Use a subtle progress indicator.

The user should understand approximate progress without feeling trapped
by a rigid multi-step form.

Do not use fake percentage progress.

------------------------------------------------------------------------

# 15. Planning Mode

This screen should introduce the two fundamental Triply strategies:

### Destination First

"I already know where I want to go."

### Budget First

"Help me discover where I can go."

Use two large selection cards.

The difference must be immediately understandable without reading
technical descriptions.

------------------------------------------------------------------------

# 16. Budget Input

Budget is a sensitive and important decision.

Design it with strong clarity.

Show:

-   Amount
-   Currency
-   Short explanatory context

The numeric value should be visually dominant.

Do not make the screen resemble a banking application.

The user should feel:

> "This is simply telling Triply what I can spend."

------------------------------------------------------------------------

# 17. Destination Selection

The destination screen should prioritize exploration without becoming a
social feed.

Recommended structure:

-   Search
-   Supported destination list
-   Destination cards
-   Selected state

Search results should remain clean.

Unsupported destination should be handled inline.

------------------------------------------------------------------------

# 18. Destination Suggestions

This is a signature Triply feature.

The user provided a budget and preferences, and Triply responds with
destination possibilities.

Design recommendations as decision-friendly cards.

Each recommendation should answer:

-   Where?
-   Why is it relevant?
-   What makes it fit the user's request?

Avoid displaying excessive AI explanation.

------------------------------------------------------------------------

# 19. Interests

Interests should feel expressive and enjoyable.

Use selectable chips/cards.

Each interest must have:

-   Default state
-   Selected state
-   Clear label
-   Optional supporting icon

Selected state must be distinguishable without relying only on color.

------------------------------------------------------------------------

# 20. Review & Confirm

This is a confidence screen.

Use clear sections:

-   Destination
-   Dates
-   Travelers
-   Budget
-   Interests

Each section should have an obvious **Edit** action.

The final CTA should be:

**Generate Trip**

The design should visually communicate:

> "Everything looks right. Let's build your trip."

------------------------------------------------------------------------

# 21. Generating Experience

The Generating screen must be treated as a major UX surface, not an
afterthought.

The backend latency target is not fixed, so the design must comfortably
support long waits.

The experience should:

-   Communicate progress without fake percentages
-   Reassure the user
-   Prevent accidental duplicate generation
-   Provide a clear failure state
-   Provide Retry
-   Allow Cancel if supported by the implementation

Use subtle travel-oriented motion or illustration if Stitch supports it.

Do not show fake technical logs.

------------------------------------------------------------------------

# 22. Trip Overview --- Core Experience

This is the most important screen in Triply.

It should receive the highest design attention.

The user may spend most of their time here.

## Header

Show:

-   Destination
-   Dates
-   Travelers
-   Status
-   Total estimated cost
-   Important actions

Keep the header compact enough to preserve itinerary space.

------------------------------------------------------------------------

## Itinerary Tab

Primary hierarchy:

**Day Selector** ↓ **Selected Day** ↓ **Timeline / Ordered Items**

Use a horizontal day selector.

Itinerary items should visually communicate chronology.

A subtle vertical timeline can be considered if it improves scanning and
remains Flutter-friendly.

------------------------------------------------------------------------

## Itinerary Item

Recommended information hierarchy:

1.  Time
2.  Place / activity
3.  Short description or metadata
4.  Estimated cost
5.  AI-generated indicator

Do not display long paragraphs inside every card.

Tap opens the Place / Item Detail Bottom Sheet.

------------------------------------------------------------------------

# 23. Costs Tab

Costs must feel understandable, not accounting-heavy.

Show categories:

-   Accommodation
-   Transportation
-   Food
-   Activities
-   Other

Then:

**Total Estimated Cost**

The word **Estimated** should remain visible.

Use simple visual grouping or a restrained chart only if it materially
improves comprehension.

Avoid financial-dashboard styling.

------------------------------------------------------------------------

# 24. AI-Generated Content

AI-generated content should have a subtle but consistent visual
identifier.

Recommended pattern:

**AI-generated**

or

**AI suggestion**

Do not use a giant robot icon or excessive AI branding.

The AI indicator should build trust, not distract.

------------------------------------------------------------------------

# 25. Structured Customization

MVP customization is not conversational.

The interface should support:

-   Edit item
-   Reorder
-   Remove
-   Regenerate item
-   Regenerate day

The Regenerate bottom sheet is the primary AI customization surface.

It must make the consequences clear.

Example structure:

**Regenerate** - This item - This day

Short explanation:

> Triply will create a new option while keeping your trip preferences.

Then:

**Regenerate**

------------------------------------------------------------------------

# 26. Bottom Sheets

Bottom sheets should be used for contextual actions.

Use them for:

-   Place / Item Detail
-   Regeneration options

Bottom sheets should:

-   Clearly belong to the current screen
-   Have a strong title
-   Provide concise content
-   Have clear actions
-   Be dismissible where safe

Do not turn contextual information into full navigation.

------------------------------------------------------------------------

# 27. Modals

Use modals for focused editing.

Edit Itinerary Item modal should be compact.

Avoid placing an entire page inside a modal.

The user should understand:

> "I am temporarily editing this item."

------------------------------------------------------------------------

# 28. Confirmation Dialogs

Destructive actions require clear confirmation.

Use:

-   Action title
-   Consequence
-   Cancel
-   Destructive action

Avoid ambiguous labels such as "Yes".

Prefer:

**Cancel**

**Archive Trip**

or

**Delete Trip**

depending on the approved behavior.

------------------------------------------------------------------------

# 29. My Trips

My Trips is a retrieval experience.

Prioritize:

-   Destination
-   Dates
-   Status
-   Estimated cost where useful

Use filtering for:

-   Active
-   Archived

Empty state should encourage the core action:

> No saved trips yet.

**Plan Your First Trip**

------------------------------------------------------------------------

# 30. Home

Home is the product entry hub.

The first question Home should answer:

> "What can I do here?"

Primary focus:

**Plan a Trip**

Secondary focus:

Resume/reopen recent trip when available.

Do not turn Home into:

-   News
-   Social feed
-   Hotel marketplace
-   Travel blog
-   Notification center

Those are outside the current product scope.

------------------------------------------------------------------------

# 31. Authentication

Authentication screens should communicate security and simplicity.

Avoid excessive decorative elements.

Login:

-   Email
-   Password
-   Log In

Register:

-   Name
-   Email
-   Password
-   Create Account

Use clear validation.

Never reveal whether an account exists through insecure error messaging.

------------------------------------------------------------------------

# 32. Navigation

Approved main navigation:

**HOME \| MY TRIPS \| CREATE TRIP \| PROFILE**

Create Trip is the primary action.

The navigation should remain visually stable throughout the
authenticated product.

During the wizard:

-   Hide bottom navigation
-   Use a full-screen stack
-   Preserve entered state
-   Use clear back navigation

Trip Overview is pushed onto the navigation stack.

------------------------------------------------------------------------

# 33. Motion & Animation

Motion should communicate state, not decorate the interface.

Use animation for:

-   Screen transitions
-   Selection
-   Loading
-   Bottom sheets
-   Tab changes
-   Success feedback
-   AI generation

Animation should be:

-   Short
-   Smooth
-   Purposeful
-   Interruptible where appropriate

Avoid:

-   Constant floating objects
-   Excessive parallax
-   Long splash animations
-   Distracting particle effects

------------------------------------------------------------------------

# 34. Loading States

Use skeletons when content structure is known.

Use meaningful loading visuals for AI generation.

Loading should preserve layout stability.

Avoid replacing the entire interface with an indefinite spinner when a
skeleton or contextual progress state is possible.

------------------------------------------------------------------------

# 35. Empty States

Every empty state should contain:

1.  Clear explanation
2.  Helpful visual cue where appropriate
3.  Next action

Examples:

**No saved trips** → Plan your first trip

**No destinations match your budget** → Adjust budget / explore
supported destinations

------------------------------------------------------------------------

# 36. Error States

Error messages must be:

-   Human-readable
-   Short
-   Non-technical
-   Actionable
-   Non-blaming

Never expose:

-   Stack traces
-   Database errors
-   API internals
-   Model internals

------------------------------------------------------------------------

# 37. Accessibility

Design with accessibility from the beginning.

**Ownership:** the UI/UX track owns the design intent; the Flutter/Mobile
track owns implementing these requirements in the client (`Mobile/lib/`).
See `04_TRIPLY_Team_and_Responsibilities.md` §3.

Minimum requirements:

-   Strong contrast
-   Readable typography
-   Adequate touch targets
-   Clear focus/selected states
-   Text labels for meaningful icons
-   Do not communicate important information using color alone
-   Support dynamic content lengths
-   Avoid tiny secondary text

------------------------------------------------------------------------

# 38. Localization Readiness

Triply may require multilingual expansion later.

Therefore:

-   Avoid hardcoded visual assumptions around English word length.
-   Allow longer labels.
-   Avoid fixed-width text containers.
-   Avoid text baked into images.
-   Keep UI strings separate from decorative artwork.
-   Design layouts that can survive RTL if Arabic support is introduced.

RTL readiness should be considered architecturally even if English is
the first design language.

------------------------------------------------------------------------

# 39. Responsive Mobile Rules

The UI must work across:

-   Small phones
-   Standard phones
-   Large phones

Avoid:

-   Fixed absolute positioning for core content
-   Layouts that only work at one resolution
-   Text clipping
-   Buttons that overflow
-   Cards that depend on a specific image ratio

Use adaptive spacing and scalable content.

------------------------------------------------------------------------

# 40. Design Tokens

The Flutter implementation should eventually derive reusable tokens from
this design direction.

Recommended token categories:

``` text
color.*
typography.*
spacing.*
radius.*
elevation.*
icon.*
motion.*
component.*
```

The exact numeric values may be finalized during the Stitch design
phase, but the implementation must not scatter arbitrary values
throughout the codebase.

------------------------------------------------------------------------

# 41. Component Architecture

The visual design should map naturally to reusable Flutter components.

Recommended conceptual components:

``` text
AppScaffold
AppTopBar
PrimaryButton
SecondaryButton
TextButton
AppTextField
CurrencyInput
DateSelector
TravelerCounter
SelectionCard
InterestChip
DestinationCard
DestinationSuggestionCard
TripCard
DaySelector
ItineraryItemCard
CostCategoryRow
StatusBadge
AIGeneratedBadge
ProgressIndicator
EmptyState
ErrorState
LoadingSkeleton
BottomSheet
ConfirmationDialog
```

Do not duplicate visually identical components for different screens.

------------------------------------------------------------------------

# 42. Stitch AI Design Rules

When generating screens in Stitch AI:

## Rule 01

Do not prompt Stitch to independently invent each screen.

The design system must remain consistent across generations.

## Rule 02

Establish the visual language first.

Then generate screens using that same language.

## Rule 03

Always reference:

-   Triply
-   Mobile-first
-   Existing screen specification
-   Existing component language
-   Existing color system
-   Existing typography
-   Existing spacing

## Rule 04

If Stitch proposes a new component, evaluate whether it should become a
reusable component rather than a one-off element.

## Rule 05

Do not accept visual changes simply because they look impressive.

Ask:

> Does this improve usability?

------------------------------------------------------------------------

# 43. Design Priority

Not every screen deserves equal design effort.

## Highest Priority

### 1. Trip Overview

The core product experience.

### 2. Trip Creation Wizard

The main conversion journey.

### 3. Destination Suggestions

A differentiating Triply feature.

### 4. Review & Confirm

The trust checkpoint.

### 5. Generating

Critical because AI latency may be noticeable.

## Medium Priority

-   Home
-   My Trips
-   Destination Selection
-   Interests
-   Trip Details

## Supporting

-   Login
-   Register
-   Profile
-   Dialogs
-   Bottom sheets

------------------------------------------------------------------------

# 44. Critical UX Risks

The design process must explicitly protect against:

### Risk 01 --- Wizard Fatigue

Too many steps can feel like a form.

Solution: - Short explanations - Strong progress - Clear CTAs - Good
defaults - Minimal information per step

### Risk 02 --- AI Uncertainty

Users may not understand why a destination or itinerary was generated.

Solution: - Clear AI labeling - Clear preference summary - Transparent
but concise explanations

### Risk 03 --- Cost Confusion

Users may assume prices are exact.

Solution: - Persistent Estimated labeling - Clear category breakdown -
No misleading precision

### Risk 04 --- Itinerary Overload

A generated trip can contain lots of information.

Solution: - Day selector - Strong hierarchy - Time-first scanning -
Compact cards - Progressive disclosure

### Risk 05 --- AI Generation Anxiety

Long generation time can feel like a broken app.

Solution: - Purposeful generating state - Clear failure handling -
Retry - No fake progress

------------------------------------------------------------------------

# 45. What NOT To Design

The following are outside MVP and must not appear as normal product
screens:

-   AI Chat
-   Maps
-   Notifications
-   Social features
-   Reviews
-   Payments
-   Admin dashboard
-   Hotel booking marketplace
-   Flight booking
-   Social sharing
-   Public profiles
-   Travel feed

Also do NOT create separate pages for:

-   Individual itinerary days
-   Cost breakdown
-   Place details
-   Edit itinerary item
-   Regeneration options
-   Archive/delete confirmation

These are contextual surfaces.

------------------------------------------------------------------------

# 46. MVP Design Acceptance Criteria

The design is considered successful when:

-   A first-time user understands Triply quickly.
-   The user can create a trip without confusion.
-   Both destination-first and budget-first journeys are clear.
-   The wizard feels short despite multiple inputs.
-   Review creates confidence.
-   Generation feels intentional and trustworthy.
-   Generated trips are easy to scan.
-   Costs are clearly estimated.
-   AI-generated content is distinguishable.
-   Customization is understandable without AI chat.
-   Saved trips are easy to retrieve.
-   Navigation is predictable.
-   Empty/error/loading states feel intentional.
-   The visual language is consistent across all screens.
-   The design can be implemented realistically in Flutter.

------------------------------------------------------------------------

# 47. Final Design North Star

Every design decision should pass this test:

> **Does this make planning a trip simpler, clearer, more trustworthy,
> or more enjoyable?**

If yes, keep it.

If it only makes the interface look more impressive, reconsider it.

Triply should not win because it has the most visual effects.

Triply should win because the user can say:

> **"I told it what I want, and it made planning the trip easy."**

------------------------------------------------------------------------

# 48. Handoff to Stitch AI

The next design phase should use this document together with:

-   `07_UI_PAGES`
-   Approved SRS
-   System Architecture
-   Database Design
-   Recommended Technology Stack

The order should be:

``` text
DESIGN.md
      ↓
Visual Foundation
      ↓
Core Components
      ↓
Authentication
      ↓
Home
      ↓
Trip Creation Wizard
      ↓
Review
      ↓
Generating
      ↓
Trip Overview
      ↓
Customization Surfaces
      ↓
My Trips
      ↓
Profile
      ↓
States / Edge Cases
      ↓
Full UX Consistency Review
      ↓
Flutter Handoff
```

The UI/UX team must treat `DESIGN.md` as the visual and interaction
foundation.

The product specification remains the authority for functionality and
scope.

------------------------------------------------------------------------

# 49. Final Rule

**Do not design screens. Design an experience.**

Triply's interface must make the complete journey feel like one
continuous action:

**I have an idea → Triply understands it → Triply plans it → I review it
→ I refine it → I save it.**
