## Products and categories

**View product list**

As **any role**,
**I want to** view the list of products with a category filter,
**so that** I can quickly find the product I need.
### Acceptance Criteria
* The product list loads when the window opens
* Filtering by category narrows the list without reloading the window
* If no products match the filter, a "No products found" message is shown

---

**Add a product category**

As an **Admin**,
**I want to** add a new product category,
**so that** I can manage the warehouse's product catalog.
### Acceptance Criteria
* The form requires a mandatory "Name" field
* A duplicate category name is rejected with an error message
* The "Add category" button is not visible to the Employee role

---

**Search for a product by name**

As **any role**,
**I want to** search for a product by name,
**so that** I can find it without scrolling through the whole list.
### Acceptance Criteria
* Search filters the product list as I type
* Search is case-insensitive
* Search works together with the category filter
* An empty search result shows a "No products found" message

---

**Add a product**

As an **Admin**,
**I want to** add a new product,
**so that** it can be selected in stock movements and tracked in stock levels.
### Acceptance Criteria
* The form includes: name, category, unit of measure (piece, kg, liter)
* Name, category and unit of measure are required
* The new product immediately appears in the product list
* The "Add" button is not visible to the Employee role

---

**Edit a product**

As an **Admin**,
**I want to** edit a product's name, category or unit of measure,
**so that** I can correct mistakes and keep the catalog up to date.
### Acceptance Criteria
* The edit form is pre-filled with the current values
* The same validation as when adding a product applies
* After saving, the changes are visible in the product list, stock levels and movement history
* The unit of measure cannot be changed for a product that already has stock records or movements; the system shows an explanatory message
* The "Edit" action is not visible to the Employee role

---

**Delete a product**

As an **Admin**,
**I want to** delete a product that is no longer needed,
**so that** the catalog contains only relevant items.
### Acceptance Criteria
* The system asks for confirmation before deleting
* A product that has stock records or movements cannot be deleted; the system shows an explanatory message
* A product without any related records is removed from the list
* The "Delete" action is not visible to the Employee role

---

**Edit a product category**

As an **Admin**,
**I want to** rename a category,
**so that** the catalog structure stays accurate.
### Acceptance Criteria
* The edit form is pre-filled with the current name
* The name is required and must be unique; a duplicate name is rejected with an error message
* After saving, the products of this category show the new name
* The "Edit" action is not visible to the Employee role

---

**Delete a product category**

As an **Admin**,
**I want to** delete an unused category,
**so that** the list of categories stays clean.
### Acceptance Criteria
* The system asks for confirmation before deleting
* A category that still contains products cannot be deleted; the message shows how many products it contains
* An empty category is removed from the list
* The "Delete" action is not visible to the Employee role

---

**View the list of categories**

As **any role**,
**I want to** view the list of product categories,
**so that** I understand how products are grouped.
### Acceptance Criteria
* The list of categories loads when the window opens
* The add, edit and delete actions are visible only to the Admin role

---

## Suppliers

**Add a supplier**

As an **Admin**,
**I want to** add a new supplier with contact details,
**so that** I can track the sources of incoming stock.
### Acceptance Criteria
* The form includes fields: name, phone, email
* The supplier name is required; phone and email are optional
* The new supplier immediately appears in the list
* The "Add" button is not visible to the Employee role

---

**Search for a supplier**

As **any role**,
**I want to** search for a supplier by name,
**so that** I can quickly find their contact details.
### Acceptance Criteria
* Search filters the list in real time
* Search is case-insensitive

---

**Edit a supplier**

As an **Admin**,
**I want to** edit a supplier's name and contact details,
**so that** the contact information stays up to date.
### Acceptance Criteria
* The edit form is pre-filled with the current values
* The supplier name is required; phone and email are optional
* After saving, the changes are visible in the supplier list
* The "Edit" action is not visible to the Employee role

---

**Delete a supplier**

As an **Admin**,
**I want to** delete a supplier I no longer work with,
**so that** the supplier directory stays relevant.
### Acceptance Criteria
* The system asks for confirmation before deleting
* A supplier referenced by existing stock movements cannot be deleted; the system shows an explanatory message, so the movement history is preserved
* A supplier without any movements is removed from the list
* The "Delete" action is not visible to the Employee role

---

## Invoices / stock movements

**Register a stock receipt**

As an **Employee**,
**I want to** register a stock receipt from a supplier,
**so that** the stock level updates automatically.
### Acceptance Criteria
* The form includes: movement number, date, product, quantity, supplier, warehouse
* The movement number is required and unique
* The quantity must be greater than 0
* The supplier is required for a receipt
* After saving, the stock level for the selected product and warehouse increases by the specified quantity
* The form is not accessible to the Admin role

---

**Register a stock write-off**

As an **Employee**,
**I want to** register a stock write-off,
**so that** I can reflect that the product is no longer actually available.
### Acceptance Criteria
* The form includes: movement number, date, product, quantity, warehouse (no supplier)
* The movement number is required and unique
* The quantity must be greater than 0
* A write-off greater than the current stock level is rejected with an error message
* After a successful save, the stock level decreases by the specified quantity
* The operation runs as a single transaction (the stock level is never updated partially on failure)
* The form is not accessible to the Admin role

---

**View the list of stock movements**

As **any role**,
**I want to** view the history of stock movements and filter it,
**so that** I can see what was received or written off, when and where.
### Acceptance Criteria
* The list shows: number, date, type (receipt / write-off), product, quantity, warehouse and the user who registered the movement
* The list can be filtered by date range, type and warehouse
* The list can be searched by movement number
* Opening a record shows its details in read-only mode
* The "create movement" action is visible only to the Employee role

---

## Stock, warehouses, and reporting

**View current stock levels**

As **any role**,
**I want to** view the current stock levels for each warehouse,
**so that** I know what's available.
### Acceptance Criteria
* The table shows product, warehouse, and quantity
* Filtering by warehouse narrows the table
* Products with stock below the minimum threshold are visually highlighted

---

**Add a warehouse**

As an **Admin**,
**I want to** add a new warehouse,
**so that** I can expand the storage network.
### Acceptance Criteria
* The form requires a mandatory "Name" field; the address is optional
* The new warehouse is immediately selectable in the stock movement form
* The "Add" button is not visible to the Employee role

---

**Generate a stock movement report**

As **any role**,
**I want to** generate a stock movement report for a chosen period,
**so that** I can analyze warehouse activity.
### Acceptance Criteria
* A date range (from — to) can be selected; the start date cannot be later than the end date
* The result shows total receipts and write-offs for each product and warehouse pair
* The report is available to both roles (Employee, Admin)

---

**Edit a warehouse**

As an **Admin**,
**I want to** edit a warehouse's name and address,
**so that** the warehouse information stays accurate.
### Acceptance Criteria
* The edit form is pre-filled with the current values
* The warehouse name is required; the address is optional
* After saving, the changes are visible in the warehouse list and in the warehouse selectors of other windows
* The "Edit" action is not visible to the Employee role

---

**Delete a warehouse**

As an **Admin**,
**I want to** delete a warehouse that is no longer used,
**so that** the list of warehouses stays relevant.
### Acceptance Criteria
* The system asks for confirmation before deleting
* A warehouse that has stock records or movements cannot be deleted; the system shows an explanatory message
* A warehouse without any related records is removed from the list
* The "Delete" action is not visible to the Employee role

---

**Set the minimum stock threshold**

As an **Admin**,
**I want to** set the minimum stock level for a product at a specific warehouse,
**so that** low-stock items are highlighted correctly.
### Acceptance Criteria
* The threshold can be set separately for each product and warehouse pair from the stock window
* The value must be a whole number not less than 0
* After saving, the highlighting in the stock table uses the new threshold immediately
* A stock record created by the first receipt of a product at a warehouse starts with a threshold of 0 (no warning) until it is set

---

**Generate a stock report**

As **any role**,
**I want to** generate a report of current stock levels,
**so that** I can see the overall state of the warehouses.
### Acceptance Criteria
* A single warehouse or all warehouses can be selected
* For one warehouse the result shows the quantity of each product; for all warehouses it shows the total quantity per product
* Products below the minimum threshold are visually highlighted
* The report is available to both roles (Employee, Admin)

---

**Generate a stock movement report by warehouse**

As **any role**,
**I want to** see receipts and write-offs grouped by warehouse for a chosen period,
**so that** I can compare the activity of different warehouses.
### Acceptance Criteria
* A date range (from — to) can be selected
* The result shows total receipts and total write-offs for each warehouse
* The report can be narrowed to a single warehouse
* The report is available to both roles (Employee, Admin)

---

## Authentication and roles

**Log in**

As a **system user**,
**I want to** log in with my username and password,
**so that** I get access according to my role.
### Acceptance Criteria
* An invalid username/password shows an error message
* After logging in, the main window opens with the sections available to that role

---

**Create a user**

As an **Admin**,
**I want to** create user accounts and assign them roles,
**so that** I can control access to the system.
### Acceptance Criteria
* The form includes: username, full name, password, role (Employee/Admin)
* The username, full name and password are required
* A duplicate username is rejected with an error message
* The "Users" section is not visible to the Employee role

---

**Search and filter users**

As an **Admin**,
**I want to** search users by login or full name and filter them by role,
**so that** I can quickly find the account I need.
### Acceptance Criteria
* The list of users loads when the window opens
* Search works by login or full name and is case-insensitive
* The list can be filtered by role (Employee, Admin)
* An empty result shows a "No users found" message
* The "Users" section is not visible to the Employee role

---

**Edit a user**

As an **Admin**,
**I want to** change a user's full name and role,
**so that** account details stay correct when responsibilities change.
### Acceptance Criteria
* The edit form is pre-filled with the current values
* The username cannot be changed
* The Admin role cannot be removed from the last remaining active Admin
* After saving, the changes are visible in the user list

---

**Block and unblock a user**

As an **Admin**,
**I want to** block a user account,
**so that** a former employee can no longer access the system while their movement history is preserved.
### Acceptance Criteria
* A blocked user is shown with the status "Blocked" in the user list
* A blocked user cannot log in; the login window shows a message that the account is blocked
* An Admin can unblock the account, which restores access
* An Admin cannot block their own account

---

**Delete a user**

As an **Admin**,
**I want to** delete an account created by mistake,
**so that** the list of users contains only real accounts.
### Acceptance Criteria
* The system asks for confirmation before deleting
* A user who has registered stock movements cannot be deleted; the system suggests blocking the account instead
* An Admin cannot delete their own account
* An account without any registered movements is removed from the list

---

**See the main menu for my role**

As a **system user**,
**I want to** see only the sections available to my role in the main menu,
**so that** I am not offered actions I cannot perform.
### Acceptance Criteria
* An Employee sees: Products, Categories, Suppliers, Warehouses, Movements, Stock, Reports
* An Admin sees the same sections plus Users
* Each menu item opens the corresponding window

---

**Handle database connection errors**

As a **system user**,
**I want to** see a clear message when the database is unavailable,
**so that** I understand what happened and no data is lost or half-saved.
### Acceptance Criteria
* If the database cannot be reached, the window shows an understandable error message instead of crashing
* A failed save leaves the data unchanged
* After the connection is restored, the operation can be repeated without restarting the application
