# Values and Representation Standards

**Applies when** the application persists domain values or exchanges them across system, file, or format boundaries — including monetary amounts, exact quantities, dates and times, machine-readable serialized values, or length-bounded and compared text. Does not apply to strictly transient in-memory logic that neither persists such values nor exchanges them across boundaries.

Defines stack-agnostic ideals for numeric precision, rounding, stored time and dates,
culture-invariant serialization, and text length and equivalence rules. Recording exact
representation contracts on both sides of a boundary in specifications lives in
`specs.md` (REPRESENTATION).

## Money and Exact Quantities

- **Never use binary floating-point for money or exact counts**: store and calculate monetary amounts and exact business quantities using base-10 fixed/decimal types or integer minor units (e.g., cents), never binary floating-point numbers whose base-2 rounding produces silent penny drift.
- **Make rounding mode explicit**: explicitly declare the rounding strategy (e.g., half-up vs half-to-even) in code to match the specification (`specs.md` REPRESENTATION) rather than inheriting a language or library default silently. Keep shared rounding policies in a single home (`standard.md` One Home per Piece of Knowledge).

## Time and Dates

- **Store and transmit instants in UTC**: record and exchange timestamps representing moments in time in UTC or with an explicit UTC offset, converting to local time zones solely at human display boundaries; local timestamps without offsets shift meaning across server locations, machine time zones, and daylight-saving transitions.
- **Keep calendar dates distinct from instants**: store and exchange pure calendar dates without a time-of-day component (e.g., due dates, birth dates, billing dates) as date values rather than local midnight timestamps, which resolve to adjacent calendar dates when read in other time zones.

## Machine-Readable Formatting

- **Use culture-invariant formats across machine boundaries**: serialize and parse numbers, booleans, dates, and times in files, network payloads, databases, and logs using an invariant, locale-independent standard format (e.g., `.` decimal separator, ISO 8601 dates/times) — never the running machine's ambient locale. Apply locale-specific formatting solely at human presentation boundaries.

## Text Length and Equivalence

- **Measure text limits in the boundary's real unit**: validate string length constraints using the exact unit enforced by the downstream store or protocol (UTF-16 code units, Unicode code points, grapheme clusters, or encoded byte length in the target encoding), explicitly accounting for multi-code-unit characters (surrogate pairs) and combining marks (`specs.md` REPRESENTATION).
- **Normalize and compare text explicitly**: apply the canonicalizations the specification defines (e.g., a Unicode normalization form; whether to fold other variants is the specification's decision) (`standard.md` External Input) explicitly prior to deduplication, indexing, or equality matching, and pass explicit comparison semantics rather than relying on ambient locale defaults.
