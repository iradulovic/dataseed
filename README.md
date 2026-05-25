# DataSeed

A schema-driven synthetic data generation CLI for .NET 10. Describe your domain in YAML — entities, relationships, quality profiles — and DataSeed generates realistic, coherent JSON datasets at configurable scale using an LLM for planning and [Bogus](https://github.com/bchavez/Bogus) for bulk record generation.

## How it works

```
dataseed plan schema.yaml   →  LLM runs once, writes schema.plan.yaml
dataseed run  schema.yaml   →  Bogus generates N records, writes output folder
```

LLM calls happen only during `plan`. The plan file is persisted so `run` is fast, deterministic (seeded), and reproducible without further API calls.

## Installation

```bash
git clone https://github.com/your-org/dataseed
cd dataseed
dotnet build dataseed.slnx
dotnet run --project src/DataSeed.Cli -- --help
```

Requires .NET 10 SDK.

## Quick start

```bash
# Scaffold a schema template
dataseed init my-domain

# Validate without LLM calls
dataseed validate my-domain.yaml

# Generate the plan (one LLM call per entity)
dataseed plan my-domain.yaml --provider claude-code

# Generate data from the plan
dataseed run my-domain.yaml
```

Output is written to a randomly named folder in the current directory (e.g. `rusty-narwhal-3f2a/`).

## Plumbing demo

The repo includes ready-to-use schemas for a plumbing distribution matching scenario — two datasets sharing the same supplier and category taxonomy but with different data quality profiles.

```bash
# Step 1: Plan the shared catalog (Supplier + ProductCategory)
dataseed plan plumbing-catalog.yaml --provider claude-code

# Step 2: Generate Dataset A (supplier-side, structured part numbers)
dataseed plan plumbing-dataset-a.yaml --provider claude-code --catalog-plan plumbing-catalog.plan.yaml
dataseed run  plumbing-dataset-a.yaml --catalog-plan plumbing-catalog.plan.yaml

# Step 3: Generate Dataset B (distributor-side, better descriptions)
dataseed plan plumbing-dataset-b.yaml --provider claude-code --catalog-plan plumbing-catalog.plan.yaml
dataseed run  plumbing-dataset-b.yaml --catalog-plan plumbing-catalog.plan.yaml
```

Each dataset produces: `Supplier.json`, `ProductCategory.json`, `Customer.json`, `Location.json`, `Product.json`, `Transaction.json`, `TransactionLine.json`, `manifest.json`.

## Commands

| Command | Description |
|---|---|
| `dataseed init <name>` | Scaffold a commented YAML schema template |
| `dataseed validate <schema.yaml>` | Validate schema structure; no LLM calls |
| `dataseed plan <schema.yaml>` | Run LLM calls, write `<schema>.plan.yaml` |
| `dataseed run <schema.yaml>` | Execute plan, write JSON output folder |

### Global options

| Option | Description |
|---|---|
| `--provider` | `claude-code` (default), `anthropic`, `openai`, `codex` |
| `--api-key` | API key for HTTP providers (falls back to env var) |
| `--model` | Override default model |
| `--format json` | Structured JSON output on stdout (for agent/tool use) |
| `--quiet` | Suppress progress output; data only |

### `plan` options

| Option | Description |
|---|---|
| `--catalog-plan <file>` | Inject resolved reference entities from a shared catalog plan |
| `--force` | Overwrite existing plan file without prompting |

### `run` options

| Option | Description |
|---|---|
| `--catalog-plan <file>` | Inject resolved reference entities from a shared catalog plan |
| `--seed <int>` | Override plan seed for reproducible output |
| `--compact` | Minified JSON output (default is pretty-printed) |

## LLM providers

| `--provider` | Description |
|---|---|
| `claude-code` | Shells out to `claude -p "..."` — uses your existing Claude subscription |
| `codex` | Shells out to `codex "..."` — uses your existing OpenAI subscription |
| `anthropic` | HTTP API — requires `--api-key` or `ANTHROPIC_API_KEY` env var |
| `openai` | HTTP API — requires `--api-key` or `OPENAI_API_KEY` env var |

LLM responses are validated as JSON and retried up to 3 times with exponential backoff.

## Schema reference

```yaml
domain: My Domain
description: >
  Context fed to the LLM for all generation calls.

entities:

  # reference — small finite set, generated entirely by LLM
  - name: Supplier
    type: reference
    count: 12
    description: Manufacturer or brand
    properties:
      - name: name
        description: Full trade name
        examples:
          - Acme Corp
      - name: code
        hints:
          - unique

  # taxonomy — hierarchical tree, LLM-generated, persisted in plan
  - name: ProductCategory
    type: taxonomy
    depth: 3
    separator: " > "
    mustInclude:
      - Plumbing > Valves > Ball Valves

  # dynamic — large record set, Bogus-generated using plan strategies
  - name: Product
    type: dynamic
    count: 2000
    qualityProfile:
      gold: 60%
      poorDescription: 30%
      missingSupplier: 10%
    properties:
      - name: sku
        hints:
          - unique
          - derived: "{Supplier.code}-{sequence:5}"
      - name: description
        hints:
          - degradable: 30%
      - name: supplierId
        ref: Supplier
        hints:
          - nullable: 10%
          - distribution: long-tail
          - skew: 0.7
      - name: categoryPath
        ref: ProductCategory
        hints:
          - depth: leaf
          - distribution: weighted
      - name: price
        hints:
          - range: 5-2500

  # child entity — count driven by parent
  - name: OrderLine
    type: dynamic
    parent: Order
    hints:
      - linesPerParent: 1-8
    properties:
      - name: productId
        ref: Product
```

### Property hints

| Hint | Meaning |
|---|---|
| `unique` | No duplicate values across all records |
| `nullable: N%` | N% of records have null value |
| `degradable: N%` | N% of string records get algorithmically degraded content |
| `derived: "template"` | Template with `{EntityName.property}` and `{sequence:N}` tokens |
| `values: [a, b, c]` | Constrained to this enum set |
| `range: min-max` | Numeric value within range |
| `distribution: random` | Uniform random assignment from referenced entity |
| `distribution: long-tail` | Power-law distribution; use with `skew: 0.0–1.0` |
| `distribution: weighted` | LLM assigns weights per taxonomy node |
| `depth: leaf` | Pick leaf-level taxonomy paths |
| `depth: N` | Pick paths at depth N |
| `linesPerParent: min-max` | Records per parent (child entities) |
| `dateRange: "YYYY-MM-DD/YYYY-MM-DD"` | Date fields drawn from this range |

### Quality profiles

```yaml
qualityProfile:
  gold: 60%           # full data, good descriptions
  poorDescription: 30% # description degraded
  missingSupplier: 10% # supplierId absent
```

Profile keys map to named property hint overrides resolved during plan generation. Each generated record is assigned a profile bucket before its properties are filled.

## Output format

```
<adjective>-<animal>-<4hex>/
  manifest.json
  Supplier.json
  ProductCategory.json
  Product.json
  ...
```

Every record includes an auto-generated `id` field (`<prefix>-<guid>`). JSON is pretty-printed by default; use `--compact` for minified output.

`manifest.json` records domain, timestamps, provider used, and record counts per entity.

## Machine-friendly usage

```bash
# All data to stdout, progress to stderr
dataseed run schema.yaml --format json --quiet

# Structured success/error JSON on stdout
# { "success": true, "outputFolder": "rusty-narwhal-3f2a" }
# { "success": false, "error": "Plan file not found" }
```

Exit codes: `0` success · `1` user error · `2` provider error · `3` internal error

## Project structure

```
src/
  DataSeed.Schema/     YAML models, parser, hint types, validator
  DataSeed.Engine/     Pipeline, plan executor, Bogus runner, output
  DataSeed.Providers/  LLM provider implementations + retry wrapper
  DataSeed.Cli/        Spectre.Console commands, DI wiring
test/
  DataSeed.Schema.Tests/
  DataSeed.Engine.Tests/
  DataSeed.Providers.Tests/
  DataSeed.Cli.Tests/
```

```bash
dotnet build dataseed.slnx
dotnet test dataseed.slnx
```

## License

MIT
