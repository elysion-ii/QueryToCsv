# Security Standards

**Applies when** the application handles secrets or personal data, or restricts data or actions per authenticated user or role. Does not apply to tools that handle neither secrets/credentials/personal data nor user/role-scoped access control.

Defines stack-agnostic ideals for preventing secret leakage, managing credentials and
passwords, encrypting sensitive data, generating cryptographic randomness, and checking
authorization. Auditing dependencies for known vulnerabilities lives in `standard.md`
(Build Hygiene and Static Analysis). Prevention of code/command/SQL injection and unsafe
type-polymorphic deserialization lives in `standard.md` (Values Never Become Code);
path-containment and archive safety live in `file-exchange.md` when present.

## Keep Secrets and Personal Data Out of Exposure Channels

- **Redact secrets and personal data from diagnostics**: never write passwords, tokens, API keys, private keys, connection credentials, or personal data into application logs, exception messages, crash dumps, or user-facing error responses (`standard.md` Failure Handling and Reporting).
- **Keep credentials out of URLs**: when transmitting authentication tokens, API keys, or session identifiers over network requests, pass them in request headers or request bodies over an encrypted channel rather than URL paths or query strings, which persist in server access logs, proxies, and browser histories (`web-api.md` Transport Encryption and Credentials, when present). This does not prohibit scoped, short-lived signed direct-transfer URLs governed by `cloud.md` (Short-Lived Signed Transfer URLs, when present).

## Secret Storage and Credential Management

- **Store secrets in platform secret stores**: hold passwords, connection strings containing credentials, API keys, and signing secrets in an OS credential vault, platform secret manager, or runtime secret injection — never hardcoded in source code or persisted in plaintext configuration files (`configuration.md`, when present). For authenticating to cloud storage or cloud services, prefer short-lived or managed cloud identities (`cloud.md`, when present).

## Data Protection at Rest and in Transit

- **Protect sensitive data in transit**: transmit sensitive data or credentials solely over encrypted, certificate-validated channels whenever crossing non-loopback or shared networks — applying `remote-calls.md` (Transport Identity and Response Boundaries, when present) for outbound client verification and `web-api.md` (Transport Encryption and Credentials, when present) for inbound API endpoints — and never disable peer certificate checks in application code.
- **Encrypt sensitive stored data with separated keys**: encrypt sensitive persisted records and backups using established authenticated encryption algorithms and manage encryption keys independently from the encrypted storage location (`cloud.md`, when present).

## Passwords and Cryptographic Randomness

- **Hash passwords with adaptive password hashing**: when storing or verifying user passwords, store them solely as salted hashes computed with an intentionally slow, memory-hard or adaptive password-hashing function (e.g., Argon2id, bcrypt, PBKDF2) — never plaintext, fast raw digests, or reversible encryption.
- **Use cryptographic randomness for security values**: generate session tokens, reset codes, nonces, salts, and unpredictable identifiers exclusively with the platform's cryptographically secure random number generator — never general-purpose pseudo-random generators whose sequences can be predicted.

## Authorization on Every Access

- **Enforce object-level authorization on a trusted boundary**: when the application serves multiple users or enforces role/user permissions, verify on a trusted boundary that the authenticated caller is authorized to access or modify every specific record or resource identified by a caller-supplied ID before reading, returning, or mutating it (`specs.md` ORIGIN). Client-side UI hiding alone is never an authorization check.
