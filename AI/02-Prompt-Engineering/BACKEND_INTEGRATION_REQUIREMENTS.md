# Backend Integration Requirements — Prompt Templates

**Purpose:** what Backend needs to build around the two prompt templates
(`prompt-templates/destination_first.md`, `prompt-templates/budget_first.md`)
so a real Gemini call can happen — the templates are text, not working code.

**References:** `AI/docs/TRIPLY_AI_JSON_SCHEMA_CONTRACT_v2.md` (the
"contract"), `json-schemas/triply-trip-plan-generation.schema.json`,
`AI/01-Dataset/DATASET_CURATION_SCHEMA_MAPPING.md`

**Status:** For Backend review/implementation — not yet built.

---

## 1. Fill in the template placeholders before every call

**What's needed:** code that takes one `Trip` row and produces the final
prompt text, by replacing every `{{PLACEHOLDER}}` in the chosen template
with a real value:

| Placeholder                                                                                                                                     | Filled from                                                                                         |
| ----------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------- |
| `{{DESTINATION_NAME}}`, `{{START_DATE}}`, `{{END_DATE}}`, `{{BUDGET_AMOUNT}}`, `{{BUDGET_CURRENCY}}`, `{{INTERESTS}}`, `{{TRIP_DURATION_DAYS}}` | The `Trip` row itself                                                                               |
| `{{PLACE_LIST_CONTEXT}}` (destination-first only)                                                                                               | `Place` rows for that one destination, `is_active = true` only                                      |
| `{{SUPPORTED_DESTINATIONS_CONTEXT}}` (budget-first only)                                                                                        | `Place` rows across **all** supported destinations, `is_active = true` only, grouped by destination |

**Why:** the templates are static text files — nothing runs them
automatically. Without this step there is no real prompt to send.

**Linked to:** template placeholder list (§ each `.md` file's header
comment); `Place`/`Destination` tables per
`DATASET_CURATION_SCHEMA_MAPPING.md` §3.6–3.7.

---

## 2. Only ever send active places

**What's needed:** the query that builds `{{PLACE_LIST_CONTEXT}}` /
`{{SUPPORTED_DESTINATIONS_CONTEXT}}` must filter `Place.is_active = true`.

**Why:** if an inactive place is ever sent to the model, the model may
return it, and it will then fail dataset grounding anyway (step 5 below) —
filtering it out earlier avoids a wasted round trip and an avoidable
`FAILED_VALIDATION`.

**Linked to:** contract §4.5 (`place_name` must resolve to an
`is_active = true` row).

---

## 3. Pick the right template by `Trip.planning_mode`

**What's needed:** a simple branch —
`Trip.planning_mode == DESTINATION_FIRST` → use `destination_first.md`;
`Trip.planning_mode == BUDGET_FIRST` → use `budget_first.md`.

**Why:** the two templates ask for structurally different things (one
destination vs. 1–3 candidate destinations) and must not be mixed up.

**Linked to:** contract §1a #6 and §4.1 (`planning_mode` field).

---

## 4. Set `destination_options.maxItems` on the schema before calling Gemini

**What's needed:** before sending the request, Backend must set the JSON
Schema's `destination_options.maxItems` to **1** for `DESTINATION_FIRST`
or **3** for `BUDGET_FIRST`. This is **not** something the prompt text
does — it has to be set programmatically on the schema object passed to
Gemini.

**Why:** without this, a `DESTINATION_FIRST` request could technically
get back more than one destination option, which contradicts the mode.

**Linked to:** contract §5, step 0 ("Request construction (pre-call)").

---

## 5. Call the Gemini API with the right generation config

**What's needed:**
- `system_instruction` = the "System Instruction" section of the chosen template (after placeholder fill)
- `contents` = the "User Prompt" section (after placeholder fill)
- `generationConfig.responseMimeType = "application/json"`
- `generationConfig.responseSchema` = the contents of `triply-trip-plan-generation.schema.json` (with `maxItems` set per step 4)

**Why:** this is what forces Gemini to return valid structured JSON
matching the agreed shape, instead of free-form text.

**Linked to:** `AI/04-Learning-Notes/Gemini_Prompt_Engineering_Upskilling_Module.md` (§1, request shape); contract §6.

---

## 6. Run the full validation pipeline on the response — never trust it as-is

**What's needed, in this exact order:**
1. **Schema-shape validation** — JSON parses, required fields present, types/enums correct.
2. **Structural consistency** — day count matches trip duration, `day_number`/`date` alignment, no duplicate slots, at least one `RESTAURANT` per day, at least one `TRANSPORT` per option.
3. **Dataset grounding (0% tolerance)** — every `place_name` and `destination_name` must resolve to an exact, active row in the database. A single unresolved name fails that destination option.
4. **Budget check** — computed by Backend from `Place.reference_price` (never from anything the model said). `BUDGET_FIRST` options that don't fit are dropped, not treated as a full failure — unless zero options fit.
5. **Persist** — only for the option(s) the user actually picks; `estimated_cost` is copied from `Place.reference_price`, never from the model.
6. **On any failure** — set `AIGeneration.status = FAILED_VALIDATION`, record what failed, retry within the agreed bound, and never show a partial or fabricated itinerary.

**Why:** the templates tell the model what rules to follow, but nothing
stops the model from breaking them (inventing a place name, proposing a
price, etc.). This pipeline is the actual enforcement layer — the
prompt is a request, this is the guarantee.

**Linked to:** contract §5 (full validation pipeline) and Design
Principle 7, "Fail closed" (§2).

---

## 7. Never take an ID or a price from the model's response

**What's needed:** `Place.id` and `Destination.id` are always resolved
server-side by exact-name lookup — never read from the response (the
schema doesn't even include them). Every cost figure shown to the user or
written to the DB comes from `Place.reference_price`, looked up after
grounding — never from the model's output.

**Why:** this is the core trust boundary of the whole contract — the
model is treated as untrusted input for anything structural or
financial; it only supplies names and ordering.

**Linked to:** contract §2, Design Principles 1 and 3.

---

*This document describes requirements for Backend to implement; it is not
itself the implementation. Once built, this file should be updated to
link to the actual Backend module/PR that does each step.*