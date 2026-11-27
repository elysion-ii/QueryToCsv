# File Exchange Standards

**Applies when** the application reads or writes files exchanged with people or other programs — e.g., user uploads, data imports, report exports, shared-folder transfers, or archive extraction. Does not apply to the application's own internal configuration files (`configuration.md`, when present), local diagnostic logs (`client.md`, when present), or private internal application state files.

Defines stack-agnostic ideals for validating external files, preventing path traversal,
bounding sizes, handling encodings and structured formats safely, and staging file I/O
across unreliable network paths. General input canonicalization and rejection live in
`standard.md` (External Input), and injection rules live in `standard.md` (Values Never
Become Code).

## Input File Validation

- **Verify file type by content, not extension**: inspect actual file headers, magic signatures, or strict format parsing rather than trusting user-supplied filenames or MIME labels.
- **Validate final extension strictly**: when filtering by extension, compare only the substring after the final `.` case-insensitively and reject filenames containing trailing dots, trailing spaces, control characters, or alternate stream delimiters (`:`), which bypass naive extension checks.
- **Validate names against platform limits**: check filenames generated or accepted for storage against target-platform restrictions — forbidden characters, reserved device names (e.g., `CON`, `PRN`, `NUL` on Windows), and path-length constraints — at the boundary.

## Path Containment

- **Enforce canonical base-directory containment**: whenever assembling a file path from any external filename or archive entry (e.g., ZIP entry paths), resolve the target path to its canonical absolute form and verify it starts strictly with the canonical intended base directory terminated by a directory separator before opening, writing, or extracting. Never trust string concatenation or naive `..` substring checks.

## Size Bounds and Streaming

- **Bound input and archive sizes upfront**: enforce explicit maximum byte limits before buffering or unpacking external files, and bound total decompressed byte count, expansion ratio, and entry count while extracting archives to prevent memory and disk exhaustion.
- **Stream large files**: read and write large or unbounded files incrementally via bounded streams or buffers rather than loading whole file contents into memory.

## Encoding and Structured Formats

- **Specify character encoding explicitly**: pass an explicit text encoding on every file read and write rather than relying on platform ambient defaults, and reject or fail on invalid byte sequences (`standard.md` External Input). When producing files for a specific consumer contract (e.g., UTF-8 with BOM for spreadsheet tools or a partner-mandated encoding), write the exact encoding specified (`specs.md` REPRESENTATION).
- **Use compliant structured parsers and serializers**: read and emit CSV, JSON, XML, and other structured formats through compliant libraries that properly escape embedded delimiters, newlines, and quotes rather than manual string splitting or concatenation.
- **Neutralize spreadsheet formula triggers**: when exporting untrusted or user-supplied text fields into CSV or spreadsheet files, neutralize leading formula-trigger characters (`=`, `+`, `-`, `@`, tab, carriage return) according to specification so spreadsheets cannot execute exported values as formulas (`standard.md` Values Never Become Code).

## Network-Path Staging and Temporary Files

- **Stage network file I/O locally**: when source or destination files may reside on network shares or removable storage, copy input files to a local temporary working directory before reading and build output files locally before atomically placing or moving the completed artifact to its final destination (`standard.md` Writes That Hold Together) — preventing link latency from stalling operations and dropped links from leaving truncated files behind.
- **Clean up on all exit paths**: delete temporary working files on every exit path, including failures; failure to delete a temporary file is best-effort (log and continue without discarding primary operation outcomes).
- **Recover crash-orphaned temp files**: because process termination skips normal exit cleanup, tag temporary files with an application-owned naming prefix or subdirectory and periodically delete leftover files belonging strictly to this application once older than a bounded retention age (e.g., at startup or loop boundaries). Never sweep shared system temporary directories indiscriminately.
