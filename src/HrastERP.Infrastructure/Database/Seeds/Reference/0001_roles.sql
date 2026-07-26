-- Reference seed: predefined RBAC roles
-- Idempotent: ON CONFLICT ("Id") DO NOTHING

INSERT INTO "Roles" ("Id", "Name", "Description", "Permissions")
VALUES
    ('00000000-0000-0000-0000-000000000001', 'Administrator',        'Full access to all modules',                    1048575),
    ('00000000-0000-0000-0000-000000000002', 'ProcurementOperator',  'Access to procurement module operations',       61696),
    ('00000000-0000-0000-0000-000000000003', 'ProductionWorker',     'Access to production module operations',        983296),
    ('00000000-0000-0000-0000-000000000004', 'WarehouseEmployee',    'Access to inventory/warehouse operations',      7936),
    ('00000000-0000-0000-0000-000000000005', 'FinanceEmployee',      'Access to finance module operations',           4592)
ON CONFLICT ("Id") DO NOTHING;
