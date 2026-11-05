-- Reference seed: super admin tenant
-- Idempotent: ON CONFLICT (id) DO NOTHING

INSERT INTO administration.tenants (
    id,
    name,
    is_active,
    created_at,
    created_by
)
VALUES (
    '00000000-0000-0000-0000-100000000000',
    'Super Admin',
    true,
    NOW(),
    '00000000-0000-0000-0000-000000000000'
)
ON CONFLICT (id) DO NOTHING;
