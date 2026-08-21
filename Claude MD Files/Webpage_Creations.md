# Revit26_Plugin — Showcase Page Project Instructions

Standing instructions for generating combined Tool Showcase + Time-Comparison
HTML deliverables for each plugin in the Revit26_Plugin suite.

---

## Purpose & Context

Building a suite of Revit 2026 automation plugins under the **Revit26_Plugin**
project. For each plugin, the deliverable is a **single self-contained HTML
showcase page** combining: Hero → compact top comparison strip → Tool
Showcase → full time-comparison section.

Pages follow a shared **navy corporate design system**:
- `--navy: #1E3A5F`
- `--danger: #E74C3C` (manual/red)
- `--accent2: #27AE60` (plugin/green)

**Workflow role:** read plugin source code (C#/XAML) first, infer everything
possible, then ask only targeted questions for what cannot be inferred —
primarily manual time baselines, project volume, and plugin runtime when no
`RunDuration_sec` telemetry exists in the codebase.

---

## Combined Deliverable — Page Order

Single scrollable HTML page, one CSS token set (no mixed color systems),
consistent Revit26_Plugin branding.

1. **Hero**
   - Tool name, one-line value prop, key metrics (items processed, % time
     saved, error rate)
   - Auto-generated concept image (generate every time, no need to ask):
     blue-gray realistic base + cyan-to-emerald gradient glow accent,
     sci-fi "digital twin" aesthetic, derived from what the tool actually does

2. **Metrics / Comparison Summary Card** — stays directly under Hero
   - **NEVER** placed at the bottom of the page
   - Small manual vs plugin numbers, red/green dot indicators, % reduction
     callout
   - Links down to the full comparison section

3. **Manual vs Plugin Time Comparison (full section)** — comes SECOND,
   immediately after Hero + summary strip, BEFORE Tool Showcase
   - Summary stat cards
   - Both toggles present together (not either/or):
     a. Complexity/points tier toggle (e.g. 200 / 400 / 500 pts)
     b. Volume toggle (e.g. 1 roof / 10 roofs / 100 roofs) — or the
        tool-appropriate scale (drain-count tiers, sheet counts, view counts)
   - Proportional horizontal bar comparison (see Bar Chart Style)
   - Nested filled-circle "speed multiple" visual (see Speed Multiple Visual)
   - Per-tool time breakdown table
   - Assumptions block

4. **Tool Showcase**
   - Features grid
   - How-it-works steps
   - Requirements / compatibility
   - UI mockup

5. **Tool's Own Screen Rendering**
   - Use actual XAML if uploaded
   - If not provided, reconstruct a mockup from code/description — don't
     block on asking

6. **Excel Sheet Sample Display**
   - Only if the tool has Excel export (EPPlus/ClosedXML)
   - Show sample rows/columns matching the real export structure

7. **Feature & Capability Matrix**
   - Checklist/table of features

---

## Bar Chart Style (time comparison)

- Two proportional horizontal bars stacked vertically in one card
  (not donut/ring gauges, not side-by-side bars)
- **Manual bar:** red gradient (`--danger #E74C3C` → `#F0837D`), label
  "MANUAL", red dot, value at right end
- **Plugin bar:** green gradient (`--accent2 #27AE60` → `#4CC18A`), label
  "PLUGIN", green dot, value at right end
- Both bars in light-grey track (`var(--bg2)`), rounded corners, ~22px tall
- Bar widths scaled proportionally against the largest tier's manual time
- Minimum width floor (~2–4%) so the plugin bar never fully disappears
- Below bars: live bolded callout — "Manual takes Nx longer per [unit]"
- Toggle above updates bar widths, values, and callout live via JS — no reload
- Card style: white background, `var(--border)` outline, 14px border-radius,
  ~32px padding

---

## Speed Multiple Visual (nested filled circles)

Separate card, placed directly below the bar-chart card, same comparison
section.

- Two solid FILLED circles, concentric (one centered inside the other) —
  not rings/outlines, not donut/gauge arcs
- **Outer filled circle:** manual baseline, fixed radius, red gradient
  (radial, matching `--danger` palette)
- **Inner filled circle:** plugin, green gradient (radial, matching
  `--accent2` palette), radius shrinks as the speed multiple grows
- Inner circle sizing uses a **log scale** (not linear) so it stays legible
  and never fully vanishes even at large multiples (100x+); clamp with a
  sane minimum radius and a maximum just under the outer radius
- Multiplier label ("Nx") + "FASTER" caption centered inside the inner
  circle, white text
- Legend beside the visual: swatch + label + value for both Manual
  (1x, with actual time in parentheses) and Plugin (Nx, with actual time in
  parentheses), plus a one-line explanatory note
- Updates live with the same toggles as the bar chart — no reload

---

## Baseline Assumptions (apply unless new values given)

- **Manual time:** 20 sec/point (derived from 1 hr for a 200-point roof)
- **Item volume:** 100+ roofs typical for a large project
- **Complexity tiers:** 200 / 400 / 500 pts (small/medium/large)
- **Plugin time:** 1–5 min per roof, linear interpolation 200–500 pts →
  3/4/5 min (unless a real `RunDuration_sec` figure is provided — if so, use
  the confirmed measured endpoints and linearly interpolate the middle tier)
- **Rework — Manual:** up to 5 full rework passes before shop drawing
  submission (worst case, modeled as full re-do)
- **Rework — Plugin:** 1 rework pass at as-built stage (base case); up to 5
  shown only as worst-case in assumptions, not the headline number
- **Rework headline:** always uses base case (1 pass) for the primary metric
  display

---

## General Rules

- If a standalone showcase or comparison page already exists, restyle and
  merge its content into the shared theme — don't just append it as-is
- Only ask the user for info that truly can't be derived from code:
  manual-process timing, rework rates, unclear item counts, missing
  descriptions/images
- Keep questions minimal and specific
- All output numbers (times, multipliers) must be cleanly rounded for
  display — no raw floating-point noise (e.g. "6.67 hr", not
  "6.666666666666667 hr")
- **Source-first discipline:** read all plugin source (command, viewmodel,
  services, XAML, constants, models) before asking any questions
- **Transparency on estimates:** when no `RunDuration_sec` telemetry exists,
  plugin runtime is flagged explicitly in the assumptions block as
  user-estimated or interpolated — never silently treated as measured
- **Confirmed-before-built rule:** all time figures and rework assumptions
  are confirmed before HTML is generated, not after
- **Tool granularity varies:** not all tools share the same complexity
  scale — use the scale appropriate to each tool's operational granularity
  (per-point tiers, per-roof drain-count tiers, sheet counts, view counts,
  etc.)
- **No duplicate mockups:** if a post-run metrics panel is placed in a
  dedicated section, remove it from the Tool Showcase rather than
  duplicating it

---

## Tools & Resources

- Plugin stack: C# / WPF / XAML, Autodesk Revit 2026 API
- Output: self-contained `.html` files with all CSS and JS inline
- Hero visuals: CSS/SVG graphics in the navy/cyan/emerald palette
  (photorealistic image generation is not available)
