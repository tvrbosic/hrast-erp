-- Fixture seed: development admin user (password: "admin")
-- Idempotent: ON CONFLICT ("Id") DO NOTHING
-- WARNING: for development use only — do NOT run in production

INSERT INTO "AspNetUsers" (
    "Id",
    "UserName",
    "NormalizedUserName",
    "Email",
    "NormalizedEmail",
    "EmailConfirmed",
    "PasswordHash",
    "SecurityStamp",
    "ConcurrencyStamp",
    "PhoneNumber",
    "PhoneNumberConfirmed",
    "TwoFactorEnabled",
    "LockoutEnd",
    "LockoutEnabled",
    "AccessFailedCount",
    "TenantId",
    "FirstName",
    "LastName",
    "IsActive",
    "RoleId"
)
VALUES (
    '00000000-0000-0000-0000-000000000010',
    'admin',
    'ADMIN',
    'admin@hrasterp.local',
    'ADMIN@HRASTERP.LOCAL',
    true,
    'AQAAAAIAAYagAAAAEJIVO6u1FOVyV1sLTlzD76MjtKJl0Wg7ng+h/LVfCpJl9r4JAPRGM4x+JpCjQikenw==',
    'STATIC-SECURITY-STAMP-DEV-ADMIN-001',
    '00000000-0000-0000-0000-000000000011',
    NULL,
    false,
    false,
    NULL,
    false,
    0,
    '00000000-0000-0000-0000-000000000000',
    'Admin',
    'User',
    true,
    '00000000-0000-0000-0000-000000000001'
)
ON CONFLICT ("Id") DO NOTHING;
