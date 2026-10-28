---
name: CADAutomate Precision System
colors:
  surface: '#f8f9ff'
  surface-dim: '#cbdbf5'
  surface-bright: '#f8f9ff'
  surface-container-lowest: '#ffffff'
  surface-container-low: '#eff4ff'
  surface-container: '#e5eeff'
  surface-container-high: '#dce9ff'
  surface-container-highest: '#d3e4fe'
  on-surface: '#0b1c30'
  on-surface-variant: '#434655'
  inverse-surface: '#213145'
  inverse-on-surface: '#eaf1ff'
  outline: '#737686'
  outline-variant: '#c3c6d7'
  surface-tint: '#0053db'
  primary: '#004ac6'
  on-primary: '#ffffff'
  primary-container: '#2563eb'
  on-primary-container: '#eeefff'
  inverse-primary: '#b4c5ff'
  secondary: '#565e74'
  on-secondary: '#ffffff'
  secondary-container: '#dae2fd'
  on-secondary-container: '#5c647a'
  tertiary: '#006242'
  on-tertiary: '#ffffff'
  tertiary-container: '#007d55'
  on-tertiary-container: '#bdffdb'
  error: '#ba1a1a'
  on-error: '#ffffff'
  error-container: '#ffdad6'
  on-error-container: '#93000a'
  primary-fixed: '#dbe1ff'
  primary-fixed-dim: '#b4c5ff'
  on-primary-fixed: '#00174b'
  on-primary-fixed-variant: '#003ea8'
  secondary-fixed: '#dae2fd'
  secondary-fixed-dim: '#bec6e0'
  on-secondary-fixed: '#131b2e'
  on-secondary-fixed-variant: '#3f465c'
  tertiary-fixed: '#6ffbbe'
  tertiary-fixed-dim: '#4edea3'
  on-tertiary-fixed: '#002113'
  on-tertiary-fixed-variant: '#005236'
  background: '#f8f9ff'
  on-background: '#0b1c30'
  surface-variant: '#d3e4fe'
typography:
  display-lg:
    fontFamily: Inter
    fontSize: 44px
    fontWeight: '700'
    lineHeight: 52px
    letterSpacing: -0.025em
  display-lg-mobile:
    fontFamily: Inter
    fontSize: 32px
    fontWeight: '700'
    lineHeight: 40px
    letterSpacing: -0.02em
  headline-lg:
    fontFamily: Inter
    fontSize: 32px
    fontWeight: '600'
    lineHeight: 40px
    letterSpacing: -0.02em
  headline-lg-mobile:
    fontFamily: Inter
    fontSize: 26px
    fontWeight: '600'
    lineHeight: 34px
    letterSpacing: -0.015em
  headline-md:
    fontFamily: Inter
    fontSize: 24px
    fontWeight: '600'
    lineHeight: 32px
    letterSpacing: -0.015em
  headline-sm:
    fontFamily: Inter
    fontSize: 18px
    fontWeight: '600'
    lineHeight: 26px
    letterSpacing: -0.01em
  body-lg:
    fontFamily: Inter
    fontSize: 16px
    fontWeight: '400'
    lineHeight: 24px
    letterSpacing: -0.005em
  body-md:
    fontFamily: Inter
    fontSize: 14px
    fontWeight: '400'
    lineHeight: 20px
    letterSpacing: 0em
  body-sm:
    fontFamily: Inter
    fontSize: 12px
    fontWeight: '400'
    lineHeight: 16px
    letterSpacing: 0.005em
  code-md:
    fontFamily: JetBrains Mono
    fontSize: 13px
    fontWeight: '500'
    lineHeight: 20px
    letterSpacing: -0.01em
  code-sm:
    fontFamily: JetBrains Mono
    fontSize: 11px
    fontWeight: '500'
    lineHeight: 16px
    letterSpacing: 0em
  label-md:
    fontFamily: JetBrains Mono
    fontSize: 12px
    fontWeight: '600'
    lineHeight: 16px
    letterSpacing: 0.04em
  label-sm:
    fontFamily: JetBrains Mono
    fontSize: 10px
    fontWeight: '600'
    lineHeight: 14px
    letterSpacing: 0.06em
rounded:
  sm: 0.25rem
  DEFAULT: 0.5rem
  md: 0.75rem
  lg: 1rem
  xl: 1.5rem
  full: 9999px
spacing:
  gutter: 1.5rem
  gutter-sm: 1rem
  margin: 2rem
  margin-sm: 1rem
  space-xs: 0.25rem
  space-sm: 0.5rem
  space-md: 1rem
  space-lg: 1.5rem
  space-xl: 2.5rem
---

## Brand & Style

### Product positioning

ATLAS CAD is the platform and discovery hub, not the name of a single AutoCAD
plugin. The catalog contains independent libraries focused on specific jobs in
the daily workflow of engineers, designers, and drafters. SteelFlow is the first
reference library in this ecosystem.

Product-facing interfaces should reinforce this model:

- lead with the breadth of the hub before highlighting an individual library;
- treat libraries as products with their own purpose, compatibility, plans,
  documentation, releases, and installation actions;
- keep discovery vocabulary consistent: **hub** for the platform,
  **marketplace/catalog** for browsing, and **library** for each installable
  solution;
- present cross-library account management as a core benefit: one account for
  subscriptions, billing, downloads, licenses, and activated devices;
- avoid messaging that makes SteelFlow appear to be the whole ATLAS CAD
  product.

This design system embodies high-precision technical modernism tailored for CAD professionals, computational design engineers, and BIM workflow managers. The personality is disciplined, architectural, and razor-sharp—balancing the rigorous exactness of industrial drafting with the ergonomic fluency of contemporary developer tooling. 

The interface evokes confidence, reliability, and algorithmic speed. Visual noise is strictly eliminated to prioritize dense script metadata, command-line arguments, execution logs, and parametric controls. Subtle drafting conventions—such as hairline geometric grids, precise hairline dividers, and monospaced code blocks—anchor the user in a native CAD environment while maintaining supreme clarity and spatial rhythm.

## Colors

The palette establishes an authentic technical aesthetic rooted in architectural drafting blueprints and clean room engineering workstations.

- **Primary Blueprint Cobalt (`#2563EB`, Hover: `#1D4ED8`):** Applied deliberately to core calls to action, focus rings, selected script nodes, and primary interactive states.
- **Structural Slate Navy (`#0F172A`):** The primary text and structural framing tone. It provides uncompromising contrast against light backdrops, ensuring legibility across high-density data tables and terminal outputs.
- **Active Emerald (`#10B981`):** Reserved for semantic verification: active enterprise licenses, verified LISP scripts, script execution success, and online automation daemons.
- **Neutral & Drafting Tones (`#64748B`, Border: `#E2E8F0`, Surface: `#F8FAFC`, Base: `#FFFFFF`):** Base canvas surfaces rely on pure `#FFFFFF` for primary cards and `#F8FAFC` for page canvases and workspace panels. Hairline boundaries leverage slate `#E2E8F0` to echo drafting grid lines without overpowering content.

## Typography

The typographic hierarchy implements a high-utility dual pairing:

- **Primary Interface (Inter):** Deployed for all proportional interface elements, headings, documentation body text, and modal dialogs. Inter delivers clear legibility in neutral contexts and dense parametric dashboards.
- **Precision Data & Code (JetBrains Mono):** Reserved for technical metadata, AutoLISP routines, script hashes, keyboard macro bindings, AutoCAD version tags (`v2024.1.2`), and API parameter signatures. JetBrains Mono’s increased x-height and clear glyph differentiation prevent critical syntax confusion (such as distinguishing `0`, `O`, `l`, and `1`).
- All code and label styles enforce monospace alignment to maintain vertical parity across multi-line terminal logs and script inspect panels.

## Layout & Spacing

This design system uses an 8px base grid rhythm with a 4px half-step for micro-alignments in technical tables, badge indicators, and toolbars.

### Grid & Breakpoints
- **Desktop (1280px+):** 12-column responsive fluid grid with `margin: 2rem` and `gutter: 1.5rem`. Max container constraint spans 1440px for marketplace views and fluid full-width for the visual script editor.
- **Tablet (768px – 1279px):** 8-column layout with `margin: 1.5rem` and `gutter: 1rem`. Script inspector collates into collapsible secondary slide-over sheets.
- **Mobile (< 768px):** 4-column layout with `margin: 1rem` and `gutter: 1rem`. Script parameters and batch runs switch to stacked vertical workflows.

### Blueprint Canvas Pattern
Workspace panels and header hero zones may utilize a faint architectural grid background composed of a 24px linear grid using `rgba(226, 232, 240, 0.45)`, giving an authentic CAD viewport feel without impeding foreground legibility.

## Elevation & Depth

Visual depth is achieved through layered structural surfaces, crisp hairline borders, and diffuse multi-layered drop shadows tinted with structural navy:

- **Surface Level 0 (Base Canvas):** `#F8FAFC` background representing the main desktop canvas.
- **Surface Level 1 (Card / Container):** Solid `#FFFFFF` bound by a persistent 1px border of `#E2E8F0`. Shadow: `0 1px 3px 0 rgba(15, 23, 42, 0.05), 0 1px 2px -1px rgba(15, 23, 42, 0.05)`.
- **Surface Level 2 (Hover / Active Cards / Menus):** `#FFFFFF` paired with border `#CBD5E1`. Shadow: `0 10px 15px -3px rgba(15, 23, 42, 0.06), 0 4px 6px -4px rgba(15, 23, 42, 0.04)`.
- **Surface Level 3 (Dialogs / Script Inspector Overlays):** `#FFFFFF` with border `#94A3B8`. Shadow: `0 20px 25px -5px rgba(15, 23, 42, 0.08), 0 8px 10px -6px rgba(15, 23, 42, 0.04)`.
- **Terminal & Code Wells:** Inset depth constructed via an interior background of `#0F172A` (or light contrast `#F1F5F9`) with an inner boundary `inset 0 1px 2px rgba(15, 23, 42, 0.1)`.

## Shapes

The design system employs a calibrated roundedness scale (`roundedness: 2` base) that merges the precision of engineering instruments with the approachability of modern SaaS software:

- **Cards & Primary Panels:** `rounded-xl` (1.5rem / 24px) creates distinct macro-groupings across the script marketplace, providing a finished, architectural enclosure.
- **Buttons, Form Inputs, & Dropdowns:** `rounded-lg` (1rem / 16px) or base `rounded` (0.5rem / 8px) depending on density requirements. Primary CTAs use 8px–10px radii for a firm, clickable affordance.
- **Status Pills, Macro Badges, & Chips:** `rounded-md` (0.375rem / 6px) to maintain a compact, technical chip aesthetic rather than pill shapes, preventing visual clutter in dense data grids.

## Components

### Buttons
- **Primary Action (Blueprint Cobalt):** `#2563EB` solid fill, white text, 1px border of `transparent`, hover state `#1D4ED8`. Focus: 2px offset with 2px ring `#2563EB`.
- **Secondary (Technical Outline):** `#FFFFFF` fill, text `#0F172A`, 1px border `#E2E8F0`. Hover: background `#F8FAFC`, border `#CBD5E1`.
- **Destructive:** `#EF4444` solid fill or outline with `#DC2626` hover.
- **Terminal Execution (Run Script):** `#0F172A` background, text `#10B981` (monospaced), hover `#1E293B` with emerald pulsing indicator.

### Input Fields & Search Bars
- Standard height: 40px for forms; 32px for compact table filters.
- Background: `#FFFFFF`, border: 1px `#E2E8F0`, corner radius: 8px.
- Text: `Inter` 14px `#0F172A`. Placeholder: `#94A3B8`.
- Focus state: border `#2563EB`, outer ring `3px rgba(37, 99, 235, 0.15)`.
- Script search inputs include a leading monospaced prefix tag (e.g., `cmd >`) in JetBrains Mono.

### Cards & Script Tiles
- Container: Background `#FFFFFF`, 1px border `#E2E8F0`, corner radius `1.5rem` (`rounded-xl`).
- Padding: `1.5rem` (desktop) and `1rem` (mobile).
- Header contains script title, AutoCAD compatibility badge, and author avatar.
- Body provides brief utility description followed by a syntax preview container (single-line AutoLISP snippet).
- Footer features download counter, version pill, and active execution CTA.

### Badges, Tags, & Chips
- **AutoCAD Version Badge:** Background `#EFF6FF`, border `#DBEAFE`, text `#1D4ED8` (`JetBrains Mono`, 11px uppercase).
- **Active License Pill:** Background `#ECFDF5`, border `#A7F3D0`, text `#047857`, featuring a leading 6px circular `#10B981` status dot.
- **LISP / Script Type Tag:** Background `#F1F5F9`, border `#E2E8F0`, text `#475569`.

### Code & Terminal Blocks
- Background `#0F172A`, border 1px `#1E293B`, radius 12px.
- Monospace font `JetBrains Mono`, 13px, line height 20px, text color `#E2E8F0`.
- Syntax highlighting: Commands/Keywords in `#60A5FA` (blue), Strings in `#34D399` (emerald), Variables in `#FBBF24` (amber), and Comments in `#64748B`.
- Top accessory bar displaying filename, file size, copy snippet button, and batch run trigger.

### Checkboxes & Toggle Switches
- Checkbox: 16x16px, 4px corner radius, 1px border `#CBD5E1`. Checked: background `#2563EB` with white checkmark icon.
- Technical Toggle: 36x20px capsule track. Active: background `#2563EB` with an inset toggle thumb. Inactive: background `#E2E8F0`.
