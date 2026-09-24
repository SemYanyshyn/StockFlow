## Products and categories

**US-01: View product list**

As an **Employee**,
**I want to** view the list of products with a category filter,
**so that** I can quickly find the product I need.
### Acceptance Criteria
* The product list loads when the window opens
* Filtering by category narrows the list without reloading the window
* An empty search result shows a "No products found" message

---

**US-02: Add a product category**

As an **Admin**,
**I want to** add a new product category,
**so that** I can manage the warehouse's product catalog.
### Acceptance Criteria
* The form requires a mandatory "Name" field
* A duplicate category name is rejected with an error message
* The "Add category" button is not visible to the Employee role

---

## Suppliers

**US-03: Add a supplier**

As an **Admin**,
**I want to** add a new supplier with contact details,
**so that** I can track the sources of incoming stock.
### Acceptance Criteria
* The form includes fields: name, phone/email
* The supplier name is required
* The new supplier immediately appears in the list

---

**US-04: Search for a supplier**

As an **Employee**,
**I want to** search for a supplier by name,
**so that** I can quickly find their contact details.
### Acceptance Criteria
* Search filters the list in real time
* Search is case-insensitive

---

## Invoices / stock movements

**US-05: Register a stock receipt**

As an **Employee**,
**I want to** register a stock receipt from a supplier,
**so that** the stock level updates automatically.
### Acceptance Criteria
* The form includes: product, quantity, supplier, warehouse
* After saving, the stock level for the selected product and warehouse increases by the specified quantity
* The form is not accessible to the Admin role

---

**US-06: Register a stock write-off**

As an **Employee**,
**I want to** register a stock write-off,
**so that** I can reflect that the product is no longer actually available.
### Acceptance Criteria
* A write-off greater than the current stock level is rejected with an error message
* After a successful save, the stock level decreases by the specified quantity
* The operation runs as a single transaction (the stock level is never updated partially on failure)

---

## Stock, warehouses, and reporting

**US-07: View current stock levels**

As an **Employee**,
**I want to** view the current stock levels for each warehouse,
**so that** I know what's available.
### Acceptance Criteria
* The table shows product, warehouse, and quantity
* Filtering by warehouse narrows the table
* Products with stock below the minimum threshold are visually highlighted

---

**US-08: Add a warehouse**

As an **Admin**,
**I want to** add a new warehouse,
**so that** I can expand the storage network.
### Acceptance Criteria
* The form requires a mandatory "Name" field
* The new warehouse is immediately selectable in the stock movement form

---

**US-09: Generate a stock movement report**

As **any role**,
**I want to** generate a stock movement report for a chosen period,
**so that** I can analyze warehouse activity.
### Acceptance Criteria
* A date range (from — to) can be selected
* The result shows total receipts and write-offs per product
* The report is available to both roles (Employee, Admin)

---

## Authentication and roles

**US-10: Log in**

As a **system user**,
**I want to** log in with my username and password,
**so that** I get access according to my role.
### Acceptance Criteria
* An invalid username/password shows an error message
* After logging in, the main window opens with the sections available to that role

---

**US-11: Manage users**

As an **Admin**,
**I want to** create user accounts and assign them roles,
**so that** I can control access to the system.
### Acceptance Criteria
* The form includes: username, password, role (Employee/Admin)
* The "Users" section is not visible to the Employee role
