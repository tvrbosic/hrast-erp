-- Reference seed: super admin tenant
-- Idempotent: ON CONFLICT ("Id") DO NOTHING

INSERT INTO "Tenant" (
    "Id",
    "Name",
    "IsActive",
    "CreatedAt",
    "CreatedBy"
)
VALUES (
    '00000000-0000-0000-0000-100000000000',
    'Super Admin',
    true,
    NOW(),
    '00000000-0000-0000-0000-000000000000'
)
ON CONFLICT ("Id") DO NOTHING;
