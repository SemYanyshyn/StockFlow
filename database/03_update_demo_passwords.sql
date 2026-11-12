-- Opt-in: only replace the original placeholders, never real account passwords/roles/IDs.
SET client_encoding = 'UTF8';
BEGIN;
UPDATE users SET password_hash = 'pbkdf2-sha256$v1$600000$DShpvjYycl8ADY3Rfuuk8A==$qcfgplxZe36zqZ350KUsd6O8YGuEQ+vWxiftmHr7hW4='
WHERE username = 'admin' AND password_hash = 'placeholder-not-a-hash';
UPDATE users SET password_hash = 'pbkdf2-sha256$v1$600000$vxy7yG1O+MESp39AkBDnTQ==$hrpump8YZnOkZtmDz41+f5xGFYznriR1mzVHELiQyTc='
WHERE username = 'employee' AND password_hash = 'placeholder-not-a-hash';
COMMIT;
