-- Fixture seed: development tenant and its admin user (password: "admin")
-- Idempotent: ON CONFLICT (id) DO NOTHING
-- WARNING: for development use only — do NOT run in production

INSERT INTO administration.tenants (
    id,
    name,
    normalized_name,
    is_active,
    created_at,
    created_by
)
VALUES (
    '00000000-0000-0000-0000-200000000000',
    'Demo Tenant',
    'DEMO TENANT',
    true,
    NOW(),
    '00000000-0000-0000-0000-000000000000'
)
ON CONFLICT (id) DO NOTHING;

INSERT INTO administration.users (
    id,
    user_name,
    normalized_user_name,
    email,
    normalized_email,
    email_confirmed,
    password_hash,
    security_stamp,
    concurrency_stamp,
    phone_number,
    phone_number_confirmed,
    two_factor_enabled,
    lockout_end,
    lockout_enabled,
    access_failed_count,
    tenant_id,
    first_name,
    last_name,
    is_active,
    role_id
)
VALUES (
    '00000000-0000-0000-0000-200000000010',
    'tenantadmin',
    'TENANTADMIN',
    'tenantadmin@hrasterp.local',
    'TENANTADMIN@HRASTERP.LOCAL',
    true,
    'AQAAAAIAAYagAAAAEJIVO6u1FOVyV1sLTlzD76MjtKJl0Wg7ng+h/LVfCpJl9r4JAPRGM4x+JpCjQikenw==',
    'STATIC-SECURITY-STAMP-DEV-TENANT-ADMIN-001',
    '00000000-0000-0000-0000-200000000011',
    NULL,
    false,
    false,
    NULL,
    false,
    0,
    '00000000-0000-0000-0000-200000000000',
    'Tenant',
    'Admin',
    true,
    '00000000-0000-0000-0000-000000000001'
)
ON CONFLICT (id) DO NOTHING;
