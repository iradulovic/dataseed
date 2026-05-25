# DataSeed CLI — Design & Implementation Specification

## Overview

DataSeed is a general-purpose, schema-driven synthetic data generation CLI tool. Users describe a domain via a YAML schema — entities, properties, relationships, and annotations — and the engine produces realistic JSON datasets at configurable scale.

The tool is designed to:
- Be driven by any LLM agent (Claude Code, Codex, etc.) as well as used directly by humans
- Produce coherent, domain-realistic data rather than obviously synthetic records
- Support intentional data quality variation (noise, missing fields, degraded descriptions)
- Persist a generation plan so LLM calls happen once, not per record

---

## Technology Stack

| Concern | Choice |
|---|---|
| Runtime | .NET 10 (console application) |
| CLI framework | Spectre.Console.Cli |
| YAML parsing | YamlDotNet |
| Fake data generation | Bogus (Faker) |
| JSON output | System.Text.Json |
| HTTP LLM access | System.Net.Http.HttpClient |
| CLI LLM access | System.Diagnostics.Process |
| Dependency injection | Microsoft.Extensions.DependencyInjection |

---

## Project Structure

```
dataseed/
  src/
    DataSeed.Cli/           # Entry point, Spectre commands, DI wiring
    DataSeed.Engine/        # Core generation pipeline, plan executor
    DataSeed.Schema/        # YAML schema models and parser
    DataSeed.Providers/     # LLM provider implementations
  dataseed.sln
```

---

## CLI Commands

### Global Options (all commands)

| Option | Description |
|---|---|
| `--provider` | LLM provider: `claude-code`, `codex`, `anthropic`, `openai` |
| `--api-key` | API key (HTTP providers); falls back to env var |
| `--model` | Model name override (optional) |
| `--format` | Output format: `text` (default), `json` |
| `--quiet` | Suppress decorative output; data only to stdout |
| `--version` | Show version |

### Commands

```
dataseed init   <schema-name>    Scaffold a domain schema YAML template
dataseed validate <schema.yaml>  Validate schema structure without LLM calls
dataseed plan   <schema.yaml>    Generate and persist plan file (LLM calls happen here)
dataseed run    <schema.yaml>    Execute persisted plan and write JSON output
```

#### `dataseed init`
- Writes `<schema-name>.yaml` to the current directory
- Produces a commented template with all supported constructs

#### `dataseed validate`
- Parses the YAML schema
- Checks entity references, hint syntax, relationship integrity
- Exits 0 if valid, non-zero with structured errors if not
- No LLM calls; fast feedback loop for agent workflows

#### `dataseed plan`
- Reads schema YAML
- Builds entity dependency graph; topologically sorts entities
- For each reference entity: sends descriptions + examples to LLM; receives structured JSON values
- For each taxonomy entity: sends description + mustInclude + depth to LLM; receives full tree + distribution weights
- For each dynamic entity: sends property descriptions + hints to LLM; receives Bogus generation strategy per property
- Writes `<schema-name>.plan.yaml` alongside the schema file
- If plan file already exists, prompts confirmation before overwriting (or `--force` to skip)

#### `dataseed run`
- Reads schema YAML and corresponding plan YAML
- Executes Bogus generation per entity using resolved plan strategies
- Writes output to a new folder in the **current working directory**
- Folder name: `<adjective>-<animal>-<4hex>` (e.g. `rusty-narwhal-3f2a`)
- Writes one JSON file per entity plus `manifest.json`
- Exits 0 on success; structured error JSON to stdout if `--format json`

---

## LLM Provider Abstraction

### Interface

```csharp
public interface ILlmProvider
{
    Task<string> CompleteAsync(string prompt, CancellationToken ct = default);
}
```

### Implementations

#### `AnthropicHttpProvider`
- POST to `https://api.anthropic.com/v1/messages`
- Auth: `x-api-key` header
- Env var fallback: `ANTHROPIC_API_KEY`
- Default model: `claude-opus-4-5` (overridable via `--model`)

#### `OpenAiHttpProvider`
- POST to `https://api.openai.com/v1/chat/completions`
- Auth: `Authorization: Bearer` header
- Env var fallback: `OPENAI_API_KEY`
- Default model: `gpt-4o` (overridable via `--model`)

#### `ClaudeCodeCliProvider`
- Shells out: `claude -p "<prompt>"`
- Requires Claude Code CLI installed and authenticated
- Captures stdout; throws on non-zero exit code
- No API key needed — uses existing Claude subscription

#### `CodexCliProvider`
- Shells out: `codex "<prompt>"`
- Requires Codex CLI installed and authenticated
- Captures stdout; throws on non-zero exit code
- No API key needed — uses existing OpenAI subscription

### Provider Selection

Via `--provider` flag or `DATASEED_PROVIDER` env var:

| Value | Provider |
|---|---|
| `anthropic` | AnthropicHttpProvider |
| `openai` | OpenAiHttpProvider |
| `claude-code` | ClaudeCodeCliProvider |
| `codex` | CodexCliProvider |

All LLM prompts instruct the model to respond with valid JSON only. The engine wraps all LLM calls with a retry + JSON parse validation loop (max 3 attempts).

---

## YAML Schema Specification

### Top-Level Structure

```yaml
domain: <string>               # Domain display name
description: <string>          # Domain context fed to LLM for all calls
entities:
  - ...                        # List of entity definitions
```

### Entity Types

#### `reference` — Small finite set, LLM-generated in full

```yaml
- name: Supplier
  type: reference
  count: 15
  description: Manufacturer or brand supplying plumbing products
  properties:
    - name: name
      description: Realistic industrial supplier company name
      examples:
        - Watts Water Technologies
        - Mueller Industries
    - name: code
      description: Short uppercase abbreviation for part number prefixes
      hints:
        - unique
      examples:
        - WWT
        - MUI
```

#### `taxonomy` — Hierarchical tree, LLM-generated, persisted in plan

```yaml
- name: ProductCategory
  type: taxonomy
  description: >
    Hierarchical classification of plumbing and HVAC products.
    Top level reflects major trade categories.
  depth: 3
  separator: " > "
  mustInclude:
    - Plumbing > Valves > Ball Valves
    - Plumbing > Valves > Gate Valves
    - Plumbing > Fittings > Couplings
    - Plumbing > Pipe > Copper Pipe
    - HVAC > Air Distribution > Flex Duct
```

`mustInclude` paths are anchored into the tree before LLM fills the rest. LLM is instructed to build a coherent tree around these anchors.

#### `dynamic` — Large record set, Bogus-generated using plan strategies

```yaml
- name: Product
  type: dynamic
  count: 2000
  description: Plumbing product SKU with intentionally varying quality
  properties:
    - name: sku
      description: Unique product identifier
      hints:
        - unique
        - derived: "{supplier.code}-{sequence:5}"
    - name: description
      description: Product description, quality intentionally varies
      hints:
        - degradable: 20%
    - name: supplierId
      ref: Supplier
      hints:
        - nullable: 15%
        - distribution: long-tail
    - name: categoryPath
      ref: ProductCategory
      hints:
        - depth: leaf
        - distribution: weighted
```

### Property Hints Reference

| Hint | Applies to | Meaning |
|---|---|---|
| `unique` | any | No duplicate values across all records |
| `nullable: N%` | any | N% of records have null/absent value |
| `degradable: N%` | string | N% of records get algorithmically degraded content |
| `derived: template` | string | Value derived from template; `{ref.prop}` and `{sequence:N}` supported |
| `values: [a,b,c]` | string | Constrained to this enum set |
| `range: min-max` | number | Numeric value within range |
| `distribution: even` | ref | Equal count per referenced entity |
| `distribution: random` | ref | Uniform random assignment |
| `distribution: weighted` | ref | LLM assigns weights per node in plan |
| `distribution: long-tail` | ref | Power law; `skew: 0.0-1.0` controls concentration |
| `cardinality: N` or `N-M` | ref | For child relationships: records per parent |
| `depth: leaf` / `depth: N` | taxonomy ref | Which level of taxonomy tree to draw from |

### Relationships

#### Reference to entity
```yaml
- name: supplierId
  ref: Supplier
```

#### Parent-child (count driven by parent)
```yaml
- name: TransactionLine
  type: dynamic
  parent: Transaction
  hints:
    - linesPerParent: 1-8
```

#### Enum / value set
```yaml
- name: locationType
  hints:
    - values: [billto, shipto]
```

### Quality Profiles

Defined at entity level; controls distribution of degradation across records:

```yaml
- name: Product
  type: dynamic
  count: 2000
  qualityProfile:
    gold: 50%           # full data, good descriptions
    poorDescription: 30% # description degraded or vague
    missingSupplier: 20% # supplierId absent
```

`qualityProfile` keys map to named property hint combinations resolved during plan generation. The engine assigns each generated record a profile bucket before generating its properties.

### Temporal Hints

```yaml
- name: Transaction
  type: dynamic
  count: 5000
  hints:
    - dateRange: "2023-01-01/2024-12-31"
  properties:
    - name: transactionDate
      description: Date of the transaction
```

---

## Plan File Specification

Written to `<schema-name>.plan.yaml` by `dataseed plan`.

### Structure

```yaml
domain: <string>
schemaFile: <string>
generatedAt: <ISO datetime>
provider: <string>

entities:
  Supplier:
    type: reference
    resolved:
      - id: sup-001
        name: Watts Water Technologies
        code: WWT
      - id: sup-002
        name: Mueller Industries
        code: MUI

  ProductCategory:
    type: taxonomy
    separator: " > "
    resolved:
      - node: Plumbing
        children:
          - node: Valves
            children:
              - node: Ball Valves
              - node: Gate Valves
              - node: Check Valves
          - node: Fittings
            children:
              - node: Couplings
              - node: Elbows
      - node: HVAC
        children:
          - node: Air Distribution
            children:
              - node: Flex Duct
              - node: Rigid Duct
    weights:
      "Plumbing > Valves > Ball Valves": 0.12
      "Plumbing > Valves > Gate Valves": 0.07
      "Plumbing > Fittings > Couplings": 0.10
      "HVAC > Air Distribution > Flex Duct": 0.05

  Product:
    type: dynamic
    count: 2000
    propertyStrategies:
      sku:
        bogus: "derived"
        template: "{supplier.code}-{sequence:00000}"
      description:
        bogus: "Commerce.ProductDescription()"
        degradePercent: 20
        degradeStrategy: "truncate-to-noun"
      supplierId:
        bogus: "pickFrom:Supplier.id"
        nullPercent: 15
        distribution: long-tail
        skew: 0.7
      categoryPath:
        bogus: "pickFrom:ProductCategory.leafPaths"
        distribution: weighted
```

---

## Output Format

### Folder Naming

Generated in **current working directory**. Folder name pattern: `<adjective>-<animal>-<4hex>`

Examples: `rusty-narwhal-3f2a`, `hollow-magpie-b81c`, `velvet-capybara-09ad`

Use a fixed list of ~50 adjectives and ~50 animals; combine with a 4-character hex suffix from `Guid.NewGuid()`.

### Files

```
<output-folder>/
  manifest.json
  Supplier.json
  ProductCategory.json
  Product.json
  Customer.json
  Location.json
  Transaction.json
  TransactionLine.json
```

### `manifest.json`

```json
{
  "domain": "Plumbing Distribution",
  "generatedAt": "2026-05-23T10:00:00Z",
  "schemaFile": "plumbing-a.yaml",
  "planFile": "plumbing-a.plan.yaml",
  "provider": "claude-code",
  "outputFolder": "rusty-narwhal-3f2a",
  "entities": {
    "Supplier": 15,
    "ProductCategory": 52,
    "Product": 2000,
    "Customer": 300,
    "Location": 712,
    "Transaction": 3000,
    "TransactionLine": 13847
  }
}
```

### Entity JSON Files

Each file is a JSON array of objects. Every record includes an auto-generated `id` field (format: `<entity-prefix>-<ulid>`).

```json
[
  {
    "id": "sup-01J5K2M3N4P5Q6R7S8T9",
    "name": "Watts Water Technologies",
    "code": "WWT"
  }
]
```

---

## Generation Pipeline

### `dataseed plan` pipeline

```
1. Parse and validate schema YAML
2. Build entity dependency graph
3. Topological sort (reference/taxonomy before dynamic, parents before children)
4. For each reference entity:
     - Build prompt: entity description + property descriptions + examples
     - Call LLM → receive JSON array of N records
     - Validate JSON; retry up to 3x on failure
     - Write to plan
5. For each taxonomy entity:
     - Build prompt: description + mustInclude paths + depth + breadth guidance
     - Call LLM → receive full tree JSON
     - Compute/request distribution weights for leaf nodes
     - Write tree + weights to plan
6. For each dynamic entity:
     - Build prompt: entity description + per-property descriptions + hints
     - Call LLM → receive Bogus strategy JSON per property
     - Write strategies to plan
7. Write plan.yaml
```

### `dataseed run` pipeline

```
1. Parse schema YAML and plan YAML
2. Create output folder in current directory
3. For each entity in topological order:
     a. Reference entities: write resolved records from plan to JSON file
     b. Taxonomy entities: write resolved leaf paths to JSON file
     c. Dynamic entities:
          - Instantiate Bogus Faker with plan strategies
          - Apply quality profile distribution
          - Generate N records (or N-per-parent for child entities)
          - Resolve refs by picking from already-written entity files
          - Write to JSON file
4. Write manifest.json
5. Print output folder name to stdout
```

---

## Machine-Friendly Conventions

For agent/tool consumption:

- All data output goes to **stdout**; progress and logs go to **stderr**
- `--format json` produces structured JSON on stdout for all commands including errors:
  ```json
  { "success": false, "error": "Schema validation failed", "details": [...] }
  ```
- `--quiet` suppresses all stderr output
- Exit codes: `0` = success, `1` = user error (bad schema, missing file), `2` = provider error, `3` = internal error
- No interactive prompts in any command when `--quiet` is set; fail with exit code instead
- `dataseed validate` is fast and LLM-free — agents should call this before `plan`

---

## Plumbing Domain Scenario — Input Files

The following are ready-to-use schema files for Ivan's plumbing demo matching scenario. Two datasets are generated from the same domain taxonomy but with different quality profiles to simulate two distinct catalog sources.

---

### Shared: `plumbing-catalog.yaml` (catalog/reference seeding — run plan once, reuse)

```yaml
domain: Plumbing Distribution Catalog
description: >
  Industrial plumbing and HVAC product catalog for a US-based distributor.
  Products span residential and commercial plumbing, hydronic heating, and
  light commercial HVAC. Supplier naming conventions follow real industrial
  distribution patterns.

entities:

  - name: Supplier
    type: reference
    count: 12
    description: >
      Manufacturer or brand supplying plumbing and HVAC products to
      industrial distributors. Mix of large national brands and regional suppliers.
    properties:
      - name: name
        description: Full legal or trade name of the supplier
        examples:
          - Watts Water Technologies
          - Mueller Industries
          - Sioux Chief Manufacturing
          - Nibco Inc.
          - Viega LLC
      - name: code
        description: 2-4 character uppercase abbreviation used in part numbers
        hints:
          - unique
        examples:
          - WWT
          - MUI
          - SCM
          - NBC
          - VGA
      - name: tier
        description: Supplier tier in distribution hierarchy
        hints:
          - values: [national, regional, specialty]

  - name: ProductCategory
    type: taxonomy
    description: >
      Hierarchical classification of plumbing and HVAC products as used by
      industrial distributors. Top level reflects major trade categories.
      Should include approximately 5 top-level categories, 5-8 subcategories each,
      and 4-8 product types at leaf level. Include some crossover products
      between plumbing and HVAC (e.g. hydronic heating components).
    depth: 3
    separator: " > "
    mustInclude:
      - Plumbing > Valves > Ball Valves
      - Plumbing > Valves > Gate Valves
      - Plumbing > Valves > Check Valves
      - Plumbing > Valves > Pressure Relief Valves
      - Plumbing > Fittings > Couplings
      - Plumbing > Fittings > Elbows
      - Plumbing > Fittings > Tees
      - Plumbing > Fittings > Reducers
      - Plumbing > Pipe > Copper Pipe
      - Plumbing > Pipe > PVC Pipe
      - Plumbing > Pipe > PEX Tubing
      - Plumbing > Water Heaters > Tank Water Heaters
      - Plumbing > Water Heaters > Tankless Water Heaters
      - Plumbing > Pumps > Circulator Pumps
      - Plumbing > Pumps > Sump Pumps
      - HVAC > Air Distribution > Flex Duct
      - HVAC > Air Distribution > Rigid Duct
      - HVAC > Air Distribution > Diffusers
      - HVAC > Hydronic Heating > Baseboard Heaters
      - HVAC > Hydronic Heating > Expansion Tanks
      - HVAC > Controls > Thermostats
      - HVAC > Controls > Zone Valves
```

---

### Dataset A: `plumbing-dataset-a.yaml`
*Simulates a supplier-side catalog: structured part numbers, weaker marketing descriptions*

```yaml
domain: Plumbing Distribution — Dataset A
description: >
  Supplier-side product catalog. Products have structured part numbers
  and supplier attribution but descriptions tend to be technical shorthand
  rather than full marketing copy. Some products lack descriptions entirely.

entities:

  - name: Product
    type: dynamic
    count: 2000
    description: >
      Plumbing or HVAC product from a supplier catalog.
      SKUs follow supplier part number conventions.
    qualityProfile:
      gold: 40%
      poorDescription: 40%
      missingSupplier: 20%
    properties:
      - name: sku
        description: Supplier part number
        hints:
          - unique
          - derived: "{supplier.code}-{sequence:5}"
      - name: description
        description: >
          Technical product description. Gold records have full spec descriptions.
          Poor records use abbreviations, truncated copy, or single-line shorthand.
        hints:
          - degradable: 40%
      - name: supplierId
        ref: Supplier
        hints:
          - nullable: 20%
          - distribution: long-tail
          - skew: 0.7
      - name: categoryPath
        ref: ProductCategory
        hints:
          - depth: leaf
          - distribution: weighted
      - name: listPrice
        description: Supplier list price in USD
        hints:
          - range: 5-2500
          - nullable: 15%
      - name: uom
        description: Unit of measure
        hints:
          - values: [EA, FT, LF, PK, CS, BX]

  - name: Customer
    type: dynamic
    count: 300
    description: Industrial distributor or contractor purchasing plumbing supplies
    properties:
      - name: name
        description: Company name of the customer
      - name: accountNumber
        description: Customer account number
        hints:
          - unique
          - derived: "ACCT-{sequence:6}"

  - name: Location
    type: dynamic
    parent: Customer
    description: Billing or shipping address for a customer
    hints:
      - linesPerParent: 1-4
    properties:
      - name: locationType
        hints:
          - values: [billto, shipto]
      - name: locationName
        description: Location or branch name
      - name: address1
        description: Street address line 1
      - name: address2
        description: Suite, unit, or secondary address
        hints:
          - nullable: 60%
      - name: city
        description: City name, US cities
      - name: state
        description: US state abbreviation
        hints:
          - values: [AL,AZ,AR,CA,CO,CT,FL,GA,ID,IL,IN,IA,KS,KY,LA,ME,MD,MA,MI,MN,MS,MO,MT,NE,NV,NH,NJ,NM,NY,NC,ND,OH,OK,OR,PA,RI,SC,SD,TN,TX,UT,VT,VA,WA,WV,WI,WY]
      - name: zip
        description: US ZIP code

  - name: Transaction
    type: dynamic
    count: 3000
    description: Customer purchase transaction
    hints:
      - dateRange: "2023-01-01/2024-12-31"
    properties:
      - name: transactionNumber
        description: Transaction or order number
        hints:
          - unique
          - derived: "ORD-{sequence:7}"
      - name: transactionDate
        description: Date of the transaction
      - name: customerId
        ref: Customer
        hints:
          - distribution: long-tail
          - skew: 0.6
      - name: shipToLocationId
        ref: Location
        hints:
          - nullable: 5%

  - name: TransactionLine
    type: dynamic
    parent: Transaction
    description: Individual line item on a transaction
    hints:
      - linesPerParent: 1-12
    properties:
      - name: productId
        ref: Product
        hints:
          - distribution: long-tail
          - skew: 0.75
      - name: quantity
        description: Quantity ordered
        hints:
          - range: 1-100
      - name: unitPrice
        description: Actual unit price charged
        hints:
          - range: 5-2500
```

---

### Dataset B: `plumbing-dataset-b.yaml`
*Simulates a distributor-side catalog: better descriptions, inconsistent part numbers, fewer supplier attributions*

```yaml
domain: Plumbing Distribution — Dataset B
description: >
  Distributor-side product catalog. Products have better marketing descriptions
  but part numbers follow distributor internal conventions rather than supplier
  originals. Supplier attribution is less consistent. Represents the same
  product domain but expressed through a different organization's lens.

entities:

  - name: Product
    type: dynamic
    count: 1800
    description: >
      Plumbing or HVAC product from a distributor catalog.
      Internal SKUs differ from supplier part numbers.
    qualityProfile:
      gold: 55%
      poorDescription: 25%
      missingSupplier: 20%
    properties:
      - name: sku
        description: Internal distributor SKU, format differs from supplier part numbers
        hints:
          - unique
          - derived: "{sequence:8}"
      - name: description
        description: >
          Marketing or catalog product description. Gold records have full
          descriptive copy. Poor records are vague or boilerplate.
        hints:
          - degradable: 25%
      - name: manufacturerName
        description: Manufacturer name as free text, not always normalized
        hints:
          - nullable: 20%
      - name: manufacturerPartNumber
        description: Supplier part number as known to the distributor, may be incomplete
        hints:
          - nullable: 35%
      - name: categoryPath
        ref: ProductCategory
        hints:
          - depth: leaf
          - distribution: weighted
      - name: listPrice
        description: Distributor list price in USD
        hints:
          - range: 5-2500
          - nullable: 10%
      - name: uom
        description: Unit of measure
        hints:
          - values: [EA, FT, LF, PK, CS, BX]

  - name: Customer
    type: dynamic
    count: 250
    description: Contractor or end-user purchasing from the distributor
    properties:
      - name: name
        description: Company or individual customer name
      - name: accountNumber
        description: Distributor internal account number
        hints:
          - unique
          - derived: "D-{sequence:5}"

  - name: Location
    type: dynamic
    parent: Customer
    description: Billing or shipping address for a customer
    hints:
      - linesPerParent: 1-3
    properties:
      - name: locationType
        hints:
          - values: [billto, shipto]
      - name: locationName
        description: Location or branch name
        hints:
          - nullable: 30%
      - name: address1
        description: Street address line 1
      - name: address2
        hints:
          - nullable: 70%
      - name: city
        description: City name, US cities
      - name: state
        description: US state abbreviation
        hints:
          - values: [AL,AZ,AR,CA,CO,CT,FL,GA,ID,IL,IN,IA,KS,KY,LA,ME,MD,MA,MI,MN,MS,MO,MT,NE,NV,NH,NJ,NM,NY,NC,ND,OH,OK,OR,PA,RI,SC,SD,TN,TX,UT,VT,VA,WA,WV,WI,WY]
      - name: zip
        description: US ZIP code

  - name: Transaction
    type: dynamic
    count: 2500
    description: Distributor sales transaction
    hints:
      - dateRange: "2023-06-01/2025-03-31"
    properties:
      - name: transactionNumber
        description: Internal sales order number
        hints:
          - unique
          - derived: "SO-{sequence:6}"
      - name: transactionDate
        description: Date of the transaction
      - name: customerId
        ref: Customer
        hints:
          - distribution: long-tail
          - skew: 0.65

  - name: TransactionLine
    type: dynamic
    parent: Transaction
    description: Line item on a distributor sales order
    hints:
      - linesPerParent: 1-10
    properties:
      - name: productId
        ref: Product
        hints:
          - distribution: long-tail
          - skew: 0.75
      - name: quantity
        description: Quantity ordered
        hints:
          - range: 1-100
      - name: unitPrice
        description: Sold price per unit
        hints:
          - range: 5-2500
```

---

## Generation Order (for both datasets)

The engine resolves dependencies and generates in this order:

```
1. Supplier          (reference — LLM generated)
2. ProductCategory   (taxonomy — LLM generated)
3. Customer          (dynamic — Bogus)
4. Location          (dynamic, parent: Customer — Bogus)
5. Product           (dynamic — Bogus)
6. Transaction       (dynamic — Bogus)
7. TransactionLine   (dynamic, parent: Transaction — Bogus)
```

Note: `plumbing-catalog.yaml` is planned separately. Its plan output (Supplier and ProductCategory resolved records) can be referenced by both dataset schemas to ensure the two datasets share the same supplier and taxonomy vocabulary. Implement a `--catalog-plan` flag on `dataseed run` to inject resolved reference entities from a separate plan file.

---

## Example CLI Usage

```bash
# Step 1: Plan the shared catalog (LLM calls)
dataseed plan plumbing-catalog.yaml --provider claude-code

# Step 2: Generate Dataset A
dataseed plan plumbing-dataset-a.yaml --provider claude-code --catalog-plan plumbing-catalog.plan.yaml
dataseed run  plumbing-dataset-a.yaml --catalog-plan plumbing-catalog.plan.yaml

# Step 3: Generate Dataset B
dataseed plan plumbing-dataset-b.yaml --provider claude-code --catalog-plan plumbing-catalog.plan.yaml
dataseed run  plumbing-dataset-b.yaml --catalog-plan plumbing-catalog.plan.yaml

# Validate a schema without LLM calls
dataseed validate plumbing-dataset-a.yaml

# Use OpenAI HTTP API instead
dataseed plan plumbing-dataset-a.yaml --provider openai --api-key sk-...

# Agent-friendly: JSON output, quiet stderr
dataseed run plumbing-dataset-a.yaml --format json --quiet
```

---

## Notes for Implementer

1. All LLM prompts must explicitly request JSON-only responses with no prose wrapping
2. The engine must validate and retry LLM responses that fail JSON parsing (max 3 attempts, exponential backoff)
3. The `degradable` hint should implement at minimum two degradation strategies: `truncate-to-noun` (keep first 1-3 words) and `vague-substitute` (replace with generic phrase like "Plumbing fitting, various sizes")
4. The `derived` template engine must support `{ref.property}` lookups into already-generated entity records and `{sequence:N}` for zero-padded incrementing integers
5. Seed Bogus with a fixed integer seed stored in the plan file so runs are reproducible: `dataseed run --seed 42` overrides the plan seed
6. The `--catalog-plan` flag allows sharing reference entity resolution across multiple dataset schemas; resolved entities from the catalog plan take precedence over any same-named entities in the dataset schema
7. Output JSON files should be pretty-printed by default; `--compact` flag for minified output
8. Print the output folder name as the last line to stdout on successful `run` — agents capture this to know where output landed
