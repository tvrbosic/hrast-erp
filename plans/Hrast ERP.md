# **Hrast ERP**

# **DISCLAIMER**

Hrast ERP is an imaginary practice project created for educational and portfolio purposes. The project is intended to demonstrate software engineering knowledge, backend architecture design, and implementation practices using ASP.NET and related technologies.

The business processes, workflows, and operational requirements described in this document are simplified representations of real-world sawmill and wood processing operations. The system is not intended to be production-ready or immediately usable in real business environments without further domain analysis, validation, security hardening, compliance considerations, and industry-specific customization.

The primary goal of the project is to serve as a learning platform for exploring software architecture concepts, modular system design, business process modeling, and enterprise application development practices.

# **PROJECT OVERVIEW**

This document describes Hrast ERP, a SaaS platform for sawmills and wood processing companies. Each tenant represents a single company location or production facility using the platform independently. The system enables companies to:

* Order materials and track incoming deliveries  
* Manage suppliers  
* Track and manage warehouse inventory  
* Assign and move materials through production processes  
* Manage wood cutting operations and production batches  
* Track production yields, by-products, and material waste  
* Track and manage incoming and outgoing payments

The service implemented within this project will be an API-first platform designed to support the core operational processes of sawmills and wood processing companies. The system is designed as a multi-tenant application where each tenant manages its own data, users, inventory, production processes, and financial information independently from other companies using the platform.

The platform is organized into modular business areas including procurement, inventory management, production, administration, and finance, allowing the system to be expanded gradually as business requirements evolve.

# **CORE ERP MODULES**

Hrast ERP will consist of several core modules representing the primary operational areas of the system:

* Administration  
* Procurement  
* Production  
* Inventory  
* Finance

# **ADMINISTRATOR MODULE**

The Administration module is responsible for managing system configuration data, reference and lookup tables, as well as user access management within the ERP system. This module enables administrators to manage users, roles, and permissions, ensuring that employees have appropriate access to system functionality based on their responsibilities within the company.

The module should support role-based access control (RBAC), where users can be assigned one or multiple roles. Each role defines a specific set of permissions that determines which modules, features, and operations the user can access.

## 

## User and Role Management

The system should allow administrators to create, update, deactivate, and manage user accounts for employees working within the organization.

Each user account should contain:

* personal and contact information  
* assigned roles  
* account status  
* authentication credentials

Users should be able to have multiple roles assigned simultaneously in order to support employees who perform responsibilities across multiple business areas.

## Roles and Permissions

The system should support predefined system roles with associated permissions that control access to ERP modules and operations. The initial system should support the following user role types:

* **Administrator:** Users with full system access and permission to manage all modules, users, roles, and system configuration.  
* **Procurement Operators:** Users responsible for procurement activities, including supplier management, purchase orders, and goods receiving operations.  
* **Production Workers:** Users responsible for executing and monitoring production-related activities such as production batches and wood processing operations.  
* **Inventory and Warehouse Employees:** Users responsible for warehouse management, inventory tracking, stock movements, and inventory adjustments.  
* **Finance and Accounting Employees:** Users responsible for reviewing procurement costs, financial records, and business-related reporting data.

The system should allow permissions to be extended or modified in the future as the ERP platform evolves.

# **PROCUREMENT MODULE**

The Procurement module is responsible for managing the purchasing process of raw logs and other materials required for production. It represents the starting point of the operational workflow within the ERP system, as all production activities depend on the availability and quality of incoming raw materials. The module is used by tenants to maintain supplier information, create and manage purchase orders, track incoming deliveries, and record received materials into inventory. 

## Supplier Management

Supplier management feature allows creating and managing supplier profiles for companies that provide input materials required for production. Each supplier profile should contain general company information such as: **company name**, **contact details**, **address**, **tax identification number**, and **additional notes** which can be used to persist information related to supplied products, cooperation history and delivery conditions. The module should support tracking of supplier activity history, including previously completed purchase orders and received deliveries, allowing users to review procurement relationships over time.

## Purchase Orders

Purchase orders feature enables procurement employees to create purchase orders for raw logs and materials that need to be acquired from suppliers. A purchase order should contain:

* Supplier information  
* Ordered materials  
* Expected quantities or volumes  
* Agreed pricing  
* Expected delivery date  
* Additional notes or delivery instructions  
* Payment status

Each purchase order should progress through a predefined workflow representing its business lifecycle. The workflow statuses are: Draft, Approved, Ordered, Received, Closed, Cancelled. Users with appropriate permissions should be able to approve purchase orders before they are sent to suppliers through emails. Once materials are delivered and fully received, the purchase order can be marked as completed and closed. To mark purchase order as delivered should be allowed only to employees that have warehouse role assigned. The system should maintain a full history of status changes for audit and traceability purposes.

## Report generation

The Procurement module should be able to generate at least one type of report containing a list of purchases with detailed information for each purchase. The report should include the list of purchased materials, quantities purchased, material unit prices, and the total purchase cost. Report generation should support filtering by supplier and date range.

# **INVENTORY MODULE**

The Inventory module is responsible for managing and tracking all raw materials and processed wood products within the ERP system. The module provides up-to-date inventory information and ensures accurate tracking of materials throughout the entire production lifecycle \- from incoming raw logs to finished wooden products ready for storage or sale. Its primary purpose is to help warehouse employees, production workers, and management maintain accurate stock records, monitor material availability, and ensure traceability of inventory movements across the organization. The module should support inventory operations for both raw materials and finished products, while maintaining a complete history of all stock changes for auditing and reporting purposes.

## Goods Receiving

Goods receiving features should support the process of receiving raw logs and materials delivered by suppliers. When a delivery arrives, warehouse operators or authorized users should be able to record the received shipment against an existing purchase order.

During receiving, users should be able to enter:

* Actual delivered quantities or volume  
* Received wood type  
* Supplier batch reference  
* delivery date  
* Optional quality notes

Once goods are received, the system should automatically create inventory transactions and increase stock quantities in the warehouse inventory module.

The system should also establish traceability between the supplier shipment and future production batches, allowing the company to identify which supplier deliveries were used in specific production operations.

## Inventory Stock Management

The system should maintain current inventory information for all stored materials and products. For each inventory item, the system should track:

* Total current quantity in stock  
* Available quantity that can be used  
* Supplier batch reference

Inventory quantities should be automatically updated as materials are received or consumed in production. The module should provide warehouse employees with up-to-date inventory information of stock availability in order to support procurement planning and production operations.

## Inventory Transactions

Every inventory change within the system should create an immutable inventory transaction record. The transaction history should provide full traceability of stock movements and allow users to review when, why, and by whom inventory quantities were modified.

The system should support multiple transaction types, including:

* Inbound deliveries from suppliers  
* Raw material consumption during production  
* Production output of finished goods  
* Manual inventory adjustments  
* Waste or damaged material disposal

Each transaction should contain information such as:

* Transaction type  
* Material or product  
* Quantity change  
* Timestamp  
* User who performed the operation  
* Related business document or production batch

The transaction history should not allow deletion or modification of previously recorded transactions in order to preserve data integrity and auditability.

## Product Types

The system should support management of finished wood products produced by the sawmill. Finished products should include wooden planks and beams of different dimensions and specifications. Each product definition should contain information such as:

* Product name  
* Dimensions  
* Wood type  
* Measurement unit  
* Product category

Example product types may include:

* Oak plank 4000x200x25 mm  
* Pine beam 3000x100x100 mm  
* Beech plank 2500x150x40 mm

The system should allow inventory quantities to be tracked separately for each unique product specification.

## Material Types

The module should support management of raw materials used in production processes.	Raw materials primarily represent incoming logs that are later processed into finished wood products. Each material definition should contain:

* Tree type  
* Log length  
* Log diameter  
* Measurement unit  
* Material classification

Example material types may include:

* Oak log \- 4m length, 45cm diameter  
* Pine log \- 5m length, 35cm diameter

The system should support tracking inventory quantities and movements for each material type independently.

## Low Stock Monitoring

The system should support automated monitoring of inventory levels for both raw materials and finished products. Background jobs should periodically evaluate current stock quantities and detect situations where inventory falls below predefined minimum thresholds. The monitoring process should support:

* Low raw material stock detection  
* Low finished product stock detection

When low stock levels are detected, the system should generate alerts or notifications that can help procurement employees and warehouse staff react in a timely manner. This functionality is intended to reduce production interruptions caused by insufficient material availability and improve inventory planning processes.

## Inventory Status Reporting

The module should support generation of inventory status reports that provide an overview of current stock levels for raw materials and finished products. The reports should allow users to review:

* Current stock quantities  
* Available quantities  
* Inventory grouped by product or material type

Users should be able to filter reports by:

* Product type  
* Material type  
* Wood species  
* Date range

The reporting functionality should help warehouse employees and management monitor inventory availability, identify shortages, and support operational planning and decision-making. Generated reports should be available for on-screen review and prepared in a format suitable for export or printing in future system versions.

# **PRODUCTION MODULE**

The purpose of the Production module is to manage the transformation of raw logs into finished wooden products such as planks and beams of different dimensions. This is the central module of the ERP system, as it represents the core operational process of the sawmill. The module is responsible for planning, scheduling, executing, and tracking all production activities performed on raw materials throughout the manufacturing lifecycle. The module should support complete traceability of production processes, from the initial raw log input to the final finished product. It should also integrate closely with the Inventory module in order to automatically track material consumption and production output during every production step.

## Machine Management and Job Scheduling

The system should maintain information about all production machines available within the sawmill. Each machine should contain information such as:

* machine identifier  
* machine type  
* operational status  
* availability  
* assigned operators

The module should support creation and scheduling of production jobs that can be assigned to specific machines.

Production jobs should be scheduled in advance and organized based on machine availability and production priorities. The system should allow production managers and operators to assign jobs to machines and manage the execution order of scheduled work. Once a machine becomes available, operators should be able to review and manually start the next scheduled production job assigned to that machine. This functionality should help the company organize production workflows and improve machine utilization efficiency.

## 

## Production Workflow

The production process should support multiple sequential processing phases required for transforming raw logs into finished wood products. The following production phases should be supported:

* **Debarking:** This phase utilizes high-speed ring debarkers to remove bark from incoming logs. Removing the bark protects downstream equipment from damage and ensures a clean wood surface for further processing.  
* **Primary Breakdown:** Large head saws or band saws perform the initial cutting operations that transform round logs into slabs or cants suitable for further processing.  
* **Secondary Cutting:** Resaws and edgers process slabs and cants into individual boards and beams with precise widths and thicknesses according to production requirements.  
* **Drying:** Processed lumber is placed into drying kilns where temperature-controlled conditions remove moisture from the wood and stabilize the material to reduce warping and deformation.  
* **Finishing:** The final processing stage uses planers and finishing machines to smooth product surfaces and ensure finished products meet required dimensional specifications and quality standards.

## Production Batches and Jobs

The system should support a hierarchical production structure consisting of production batches and individual production jobs.

A production batch represents the complete lifecycle of transforming raw logs into finished products, while individual jobs represent specific processing operations within that lifecycle.

Each production job should contain:

* unique job identifier  
* batch identifier  
* assigned machine  
* assigned operator  
* start time  
* end time  
* production status  
* input materials  
* output products

Supported production statuses should include:

* Pending  
* Executing  
* Done

The same batch identifier should be shared across all related production jobs belonging to the same production process, allowing the system to maintain complete traceability from the first processing phase to the final finished product.

The module should support tracking relationships between:

**Batch → Individual Jobs → Input Materials → Output Products**

## Material Consumption and Inventory Integration

The Production module should integrate directly with the Inventory module. After completion of each production job, the system should automatically:

* reduce inventory quantities for consumed input materials  
* increase inventory quantities for produced output goods  
* create inventory transaction records

The system should support storage and tracking of intermediate products generated during individual production phases. Intermediate products should remain available in inventory for use in subsequent production operations, with the goal of minimizing material waste and utilizing all usable materials whenever possible.

## Production Reporting

The module should support generation of production reports and operational analytics. Reports should provide information about:

* consumed raw materials  
* produced output quantities  
* machine utilization  
* production efficiency  
* completed production jobs and batches

Users should be able to generate reports:

* per machine  
* per production batch  
* per operator  
* for a selected date range  
* for the entire production facility

The reporting functionality should help management monitor production performance and support operational decision-making processes.

## **FINANCE MODULE**

## Overview

The Finance module is responsible for tracking monetary flows related to procurement and sales operations within the ERP system. It supports:

* outgoing payments to suppliers based on purchase orders  
* incoming payments based on sales invoices  
* simple invoice generation  
* financial reporting for expenses and income

The module is intentionally simplified and does not implement full accounting or ledger functionality. Its goal is to provide operational financial visibility rather than formal bookkeeping.

## Core Design Concept

Instead of building complex accounting structures, the module is based on three simple concepts:

Purchase Orders → Supplier Invoices → Outgoing Payments

Sales Orders → Sales Invoices → Incoming Payments

## Supplier Invoices (Outgoing Side)

Supplier invoices are generated from purchase orders and represent amounts the company must pay. The system should allow automatic or manual creation of supplier invoices based on approved purchase orders. Each supplier invoice should contain:

* invoice identifier  
* related purchase order  
* supplier reference  
* issue date  
* total amount  
* status (Unpaid, Paid, Cancelled)

Supplier invoices serve as the financial representation of procurement activities.

When a supplier invoice is paid, the system should mark it as settled and record the payment transaction.

## Sales Invoices (Incoming Side)

Sales invoices represent revenue generated from selling finished wood products. The system should support simple CRUD management of sales invoices. Each sales invoice should contain:

* invoice identifier  
* customer reference (simple text or id)  
* issue date  
* total amount  
* status (Unpaid, Paid, Cancelled)

When a sales invoice is marked as paid, the system should:

* register incoming payment  
* optionally trigger inventory deduction (if goods were reserved or not yet shipped)

Sales invoices represent the financial inflow side of the ERP system.

## Transactions (Unified money flow table)

The system should store all financial transactions in a single payments table. This table represents actual money movement and is independent of invoice type. Transaction record contains:

* payment identifier  
* direction (Incoming / Outgoing)  
* related invoice id (supplier or sales)  
* amount  
* payment date  
* payment method (Stripe, bank transfer, manual entry)  
* external reference (e.g. Stripe payment id)  
* status (Pending, Succeeded, Failed)

This design ensures a single source of truth for all money movements.

## Financial Reporting

The module should support simple financial reporting based on payments. Reports should calculate:

* Expenses  
  * total outgoing payments  
  * supplier costs  
  * procurement spending  
* Income  
  * total incoming payments  
  * revenue from sales invoices  
* Period filtering  
  * date range  
  * supplier/customer filtering (optional)

Reports are generated directly from the payments table, which acts as the primary financial source of truth.

