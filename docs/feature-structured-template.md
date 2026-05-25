# Feature Spec: Structured Template Generation

## Summary

Introduce a `structuredTemplate` hint for string properties on any entity type.
Instead of Bogus generating domain-agnostic values (e.g. `Commerce.ProductDescription()`),
the LLM infers the structural anatomy of the property during `plan` — templates and
named value pools — and the `run` stage assembles unique, domain-correct strings
combinatorially at scale.

This replaces all `bogus: Commerce.*()` or similar domain-blind Bogus strategies
for human-readable string properties.

---

## Problem Statement

Bogus has no domain knowledge. Any string property that requires realistic,
domain-specific text — product descriptions, company names, location branch names,
transaction references — will produce generic or nonsensical output when mapped to
a Bogus method.

The fix must:
- Keep LLM calls at `plan` time only (not per record at `run` time)
- Produce unique values at scale through combinatorial assembly
- Work for any string property on any entity type
- Allow variation based on a related entity (e.g. description varies by category)
- Support quality variants where applicable (gold, degraded, sparse)

---

## Design

### New Hint: `structuredTemplate`

Applicable to any `string` property on any `dynamic` entity.

#### Without ref (single structure, all records)

```yaml
- name: companyName
  description: Industrial distributor or contractor company name
  hints:
    - structuredTemplate
```

LLM produces one structure applied uniformly across all records of this entity.

#### With ref (structure varies per referenced entity's values)

```yaml
- name: description
  description: Product description based on specifications
  hints:
    - structuredTemplate:
        ref: categoryPath
```

LLM produces one structure per leaf node (or value) of the referenced property.
At run time, the engine looks up the record's value for `categoryPath` and
selects the matching structure.

The `ref` must point to another property on the same entity that is itself
a `ref` to a `taxonomy` or `reference` entity.

---

## Plan File Changes

### Without ref — single structure

```yaml
Product:
  propertyStructures:
    manufacturerPartNumber:
      templates:
        default: "{prefix}-{family}{series}-{size}"
      parts:
        prefix:
          values: ["WWT", "MUI", "SCM", "NBC"]
        family:
          values: ["BV", "GV", "CV", "CP", "EL"]
        series:
          values: ["100", "200", "300", "500", "PRO"]
        size:
          values: ["0050", "0075", "0100", "0150", "0200"]
```

### With ref — structure per taxonomy leaf or reference value

```yaml
Product:
  propertyStructures:
    description:
      ref: categoryPath
      structures:
        "Plumbing > Valves > Ball Valves":
          templates:
            gold: "{size} {material} ball valve, {port} port, {pressure} WOG, {ends} ends"
            degraded: "{material} ball valve, {size}"
            sparse: "Ball valve fitting"
          parts:
            size:
              values: ["1/4 in.", "3/8 in.", "1/2 in.", "3/4 in.", "1 in.", "1-1/2 in.", "2 in."]
            material:
              values: ["brass", "stainless steel", "bronze", "PVC"]
            port:
              values: ["full", "standard", "reduced"]
            pressure:
              values: ["200", "400", "600", "1000"]
            ends:
              values: ["threaded", "solder", "push-to-connect", "flanged"]

        "Plumbing > Fittings > Couplings":
          templates:
            gold: "{size} {material} coupling, {connection} x {connection}, {standard}"
            degraded: "{material} coupling, {size}"
            sparse: "Pipe coupling"
          parts:
            size:
              values: ["1/2 in.", "3/4 in.", "1 in.", "1-1/4 in.", "2 in."]
            material:
              values: ["copper", "brass", "PVC", "galvanized steel"]
            connection:
              values: ["C", "FPT", "MPT", "push-fit"]
            standard:
              values: ["ASTM B88", "NSF 61", "lead-free compliant"]

        "HVAC > Air Distribution > Flex Duct":
          templates:
            gold: "{diameter} in. x {length} ft. flexible duct, {insulation}, {rating} rated"
            degraded: "Flex duct, {diameter} in."
            sparse: "Flexible duct"
          parts:
            diameter:
              values: ["4", "6", "8", "10", "12", "14"]
            length:
              values: ["10", "15", "25"]
            insulation:
              values: ["R-4.2", "R-6", "R-8"]
            rating:
              values: ["UL 181", "Class 1 air duct"]
```

### Template variant selection

Template variant keys (`gold`, `degraded`, `sparse`) align with the entity's
`qualityProfile` bucket names. If the entity has no `qualityProfile`, only
`default` is used.

If a property has `structuredTemplate` but the entity has no `qualityProfile`,
only the `default` template variant is generated and used for all records.

---

## Run Engine Changes

### Template resolution

For each record being generated:

1. If the property has `structuredTemplate` without `ref`:
   - Use the single structure in `propertyStructures[propertyName]`

2. If the property has `structuredTemplate` with `ref`:
   - Read the record's current value for the `ref` property (e.g. `categoryPath`)
   - Look up `propertyStructures[propertyName].structures[refValue]`
   - If no structure found for that value, fall back to the most generic
     ancestor path available in the structure map, or use a plain
     `"{entityName} {propertyName}"` fallback string

3. Select template variant based on record's assigned quality profile bucket.
   If no quality profile, use `default`.

4. For each `{part}` token in the selected template:
   - Pick a random value from `parts[part].values`
   - Same `{part}` token appearing multiple times in one template picks
     independently each time (e.g. `{connection} x {connection}` can yield
     `"FPT x MPT"`)

5. Replace all tokens; return assembled string.

### No Bogus involvement for structuredTemplate properties

Properties resolved via `structuredTemplate` do not go through Bogus.
The template resolver is a standalone component: `ITemplateResolver`.

```csharp
public interface ITemplateResolver
{
    string Resolve(
        string template,
        IReadOnlyDictionary<string, IReadOnlyList<string>> parts,
        Random rng);
}
```

The engine passes the same seeded `Random` instance used for all Bogus
generation, preserving reproducibility.

---

## Plan Generation Changes

### LLM prompt per structuredTemplate property

When the plan generator encounters a property with `structuredTemplate`, it
sends a prompt structured as follows:

**Without ref:**
```
Domain: {domain description}
Entity: {entity name} — {entity description}
Property: {property name} — {property description}

Produce a JSON structure for generating realistic {property name} values for
this entity in this domain. The structure must contain:
- "templates": object with variant keys. Always include "default". Add "degraded"
  and "sparse" variants only if the entity schema defines a qualityProfile.
- "parts": object where each key matches a {token} in the templates, with a
  "values" array of 6-12 realistic domain-specific strings.

Respond with valid JSON only. No prose.
```

**With ref (taxonomy):**
```
Domain: {domain description}
Entity: {entity name} — {entity description}
Property: {property name} — {property description}
Referenced taxonomy: {taxonomy entity name}

For each of the following taxonomy leaf paths, produce a JSON structure for
generating realistic {property name} values specific to that product type.

Taxonomy leaf paths:
{list of all leaf paths from the taxonomy plan}

The response must be a JSON object keyed by taxonomy path. Each value contains:
- "templates": object with variant keys matching the entity's qualityProfile
  bucket names (or just "default" if no qualityProfile).
- "parts": object where each key matches a {token} in the templates, with a
  "values" array of 6-12 realistic domain-specific strings for that product type.

Respond with valid JSON only. No prose.
```

**With ref (reference entity):**
Same as taxonomy variant but keyed by the reference entity's resolved record
values (e.g. supplier `code` or `name` field — use the field that makes
most sense as a key given the property description).

---

## Examples Across Entity Types

### Customer — `companyName` (no ref)

```yaml
- name: Customer
  type: dynamic
  properties:
    - name: companyName
      description: Industrial distributor or plumbing contractor company name
      hints:
        - structuredTemplate
```

Plan resolves to:
```yaml
Customer:
  propertyStructures:
    companyName:
      templates:
        default: "{city} {trade} {type}"
      parts:
        city:
          values: ["Midwest", "National", "Central", "Tri-State", "Valley", "Metro"]
        trade:
          values: ["Plumbing", "Mechanical", "Pipe", "HVAC", "Supply", "Wholesale"]
        type:
          values: ["Supply Co.", "Distributors", "Group", "Inc.", "LLC", "& Sons"]
```

Produces: `"Tri-State Plumbing Supply Co."`, `"Midwest HVAC Distributors"`, etc.

---

### Location — `locationName` (no ref)

```yaml
- name: Location
  type: dynamic
  parent: Customer
  properties:
    - name: locationName
      description: Branch or warehouse location name
      hints:
        - structuredTemplate
        - nullable: 30%
```

Plan resolves to:
```yaml
Location:
  propertyStructures:
    locationName:
      templates:
        default: "{descriptor} {type}"
      parts:
        descriptor:
          values: ["North", "South", "East", "West", "Central", "Downtown", "Airport", "Industrial"]
        type:
          values: ["Branch", "Warehouse", "Distribution Center", "Supply House", "Showroom"]
```

Produces: `"North Branch"`, `"Industrial Distribution Center"`, etc.

---

### Transaction — `notes` (no ref)

```yaml
- name: Transaction
  type: dynamic
  properties:
    - name: notes
      description: Optional order notes or special instructions
      hints:
        - structuredTemplate
        - nullable: 60%
```

Plan resolves to:
```yaml
Transaction:
  propertyStructures:
    notes:
      templates:
        default: "{instruction}"
      parts:
        instruction:
          values:
            - "Call before delivery"
            - "Deliver to back warehouse"
            - "Rush order — job site deadline"
            - "Partial shipment acceptable"
            - "Signature required on delivery"
            - "Leave at dock if no answer"
```

---

### Product — `description` with ref to taxonomy (full example already shown above)

---

## Schema Hint Compatibility

`structuredTemplate` is compatible with:
- `nullable: N%` — N% of records skip template resolution and write null
- `unique` — after resolution, if value collides with a prior record,
  re-resolve up to 5 times before appending a disambiguating suffix

`structuredTemplate` is incompatible with (engine should reject at validate time):
- `derived` — mutually exclusive generation strategies
- `values` — mutually exclusive generation strategies
- `range` — applies to numeric properties only

---

## Updated Schema Property Reference

| Hint | Applies to | Meaning |
|---|---|---|
| `structuredTemplate` | string | LLM infers template + parts at plan time; run assembles combinatorially |
| `structuredTemplate.ref` | string | Optional; structure varies per value of referenced property |

---

## Fallback Behaviour

If the plan file contains no structure for a given `ref` value encountered
at run time (e.g. a taxonomy path the LLM did not cover):

1. Walk up the path looking for a parent-level structure
   (`"Plumbing > Valves"` → `"Plumbing"`)
2. If still not found, use `"{entityName} item"` as a plain string fallback
3. Log a warning to stderr with the unresolved path

This prevents hard failures on edge cases without silently producing
obviously wrong output.

---

## Notes for Implementer

1. `ITemplateResolver` should be registered as a singleton; it is stateless
   beyond the `Random` instance passed per call
2. The seeded `Random` used by the template resolver must be the same instance
   used by Bogus generation for the same entity batch, ensuring a single seed
   controls full run reproducibility
3. When generating plan structures for taxonomy-referenced properties, send all
   leaf paths in a single LLM call rather than one call per path — this keeps
   LLM call count proportional to entity count, not taxonomy leaf count
4. The `validate` command should warn (not error) if a `structuredTemplate`
   property has no `qualityProfile` on its entity and no `default` template
   variant — this is a likely authoring mistake
5. Plan file structures should be human-editable — a user may want to hand-tune
   the value lists for a specific domain without re-running the plan step.
   The `run` command must not overwrite or re-validate structure content,
   only read it.
