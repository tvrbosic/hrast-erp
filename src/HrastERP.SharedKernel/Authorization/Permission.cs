namespace HrastERP.SharedKernel.Authorization;

[Flags]
public enum Permission : long
{
    None = 0,

    // Administration (bits 0–3)
    AdministrationView   = 1L << 0,
    AdministrationCreate = 1L << 1,
    AdministrationEdit   = 1L << 2,
    AdministrationDelete = 1L << 3,

    // Finance (bits 4–7)
    FinanceView   = 1L << 4,
    FinanceCreate = 1L << 5,
    FinanceEdit   = 1L << 6,
    FinanceDelete = 1L << 7,

    // Inventory (bits 8–11)
    InventoryView   = 1L << 8,
    InventoryCreate = 1L << 9,
    InventoryEdit   = 1L << 10,
    InventoryDelete = 1L << 11,

    // Procurement (bits 12–15)
    ProcurementView   = 1L << 12,
    ProcurementCreate = 1L << 13,
    ProcurementEdit   = 1L << 14,
    ProcurementDelete = 1L << 15,

    // Production (bits 16–19)
    ProductionView   = 1L << 16,
    ProductionCreate = 1L << 17,
    ProductionEdit   = 1L << 18,
    ProductionDelete = 1L << 19,
}
