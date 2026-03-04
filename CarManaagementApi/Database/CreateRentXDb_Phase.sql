SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF DB_ID(N'CarManagementDb') IS NULL
BEGIN
    CREATE DATABASE CarManagementDb;
END
GO

USE CarManagementDb;
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'rentx')
BEGIN
    EXEC (N'CREATE SCHEMA rentx');
END
GO

/* =========================
   PHASE 1: AUTH + LOOKUPS + MASTER
   ========================= */

IF OBJECT_ID(N'rentx.Roles', N'U') IS NULL
BEGIN
    CREATE TABLE rentx.Roles
    (
        RoleCode VARCHAR(20) NOT NULL PRIMARY KEY,
        RoleName NVARCHAR(50) NOT NULL,
        IsSystem BIT NOT NULL CONSTRAINT DF_Roles_IsSystem DEFAULT (1),
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_Roles_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT CK_Roles_RoleCode CHECK (RoleCode IN ('admin','ops','agent','viewer'))
    );
END
GO

IF OBJECT_ID(N'rentx.Users', N'U') IS NULL
BEGIN
    CREATE TABLE rentx.Users
    (
        UserId VARCHAR(20) NOT NULL PRIMARY KEY,
        Username NVARCHAR(100) NOT NULL,
        FullName NVARCHAR(120) NOT NULL,
        Email NVARCHAR(255) NOT NULL,
        Phone VARCHAR(15) NULL,
        RoleCode VARCHAR(20) NOT NULL,
        PasswordHash NVARCHAR(512) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT (1),
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAt DATETIME2(0) NULL,
        LastLoginAt DATETIME2(0) NULL,
        CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleCode) REFERENCES rentx.Roles(RoleCode)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'rentx.Users') AND name = N'UX_Users_Username')
BEGIN
    CREATE UNIQUE INDEX UX_Users_Username ON rentx.Users(Username);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'rentx.Users') AND name = N'UX_Users_Email')
BEGIN
    CREATE UNIQUE INDEX UX_Users_Email ON rentx.Users(Email);
END
GO

IF OBJECT_ID(N'rentx.UserRefreshTokens', N'U') IS NULL
BEGIN
    CREATE TABLE rentx.UserRefreshTokens
    (
        RefreshTokenId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UserId VARCHAR(20) NOT NULL,
        TokenHash NVARCHAR(512) NOT NULL,
        ExpiresAt DATETIME2(0) NOT NULL,
        RevokedAt DATETIME2(0) NULL,
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_UserRefreshTokens_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_UserRefreshTokens_Users FOREIGN KEY (UserId) REFERENCES rentx.Users(UserId)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'rentx.UserRefreshTokens') AND name = N'UX_UserRefreshTokens_TokenHash')
BEGIN
    CREATE UNIQUE INDEX UX_UserRefreshTokens_TokenHash ON rentx.UserRefreshTokens(TokenHash);
END
GO

IF OBJECT_ID(N'rentx.Branches', N'U') IS NULL
BEGIN
    CREATE TABLE rentx.Branches
    (
        BranchId VARCHAR(6) NOT NULL PRIMARY KEY,
        Name NVARCHAR(120) NOT NULL,
        Phone VARCHAR(20) NULL,
        Email NVARCHAR(255) NULL,
        Address NVARCHAR(300) NULL,
        City NVARCHAR(120) NULL,
        State NVARCHAR(50) NULL,
        Pincode CHAR(6) NULL,
        OpenAt TIME(0) NOT NULL,
        CloseAt TIME(0) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_Branches_IsActive DEFAULT (1),
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_Branches_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAt DATETIME2(0) NULL,
        CONSTRAINT CK_Branches_Id CHECK (BranchId NOT LIKE '%[^A-Z0-9]%'),
        CONSTRAINT CK_Branches_Pincode CHECK (Pincode IS NULL OR Pincode NOT LIKE '%[^0-9]%'),
        CONSTRAINT CK_Branches_Hours CHECK (CloseAt > OpenAt)
    );
END
GO

IF OBJECT_ID(N'rentx.Cars', N'U') IS NULL
BEGIN
    CREATE TABLE rentx.Cars
    (
        CarId VARCHAR(20) NOT NULL PRIMARY KEY,
        Brand NVARCHAR(80) NOT NULL,
        Model NVARCHAR(80) NOT NULL,
        CarType VARCHAR(20) NOT NULL,
        Fuel VARCHAR(20) NOT NULL,
        Transmission VARCHAR(20) NOT NULL,
        Seats TINYINT NOT NULL,
        DailyPrice DECIMAL(10,2) NOT NULL,
        RegNo VARCHAR(20) NOT NULL,
        Odometer INT NOT NULL,
        BranchId VARCHAR(6) NOT NULL,
        Rating DECIMAL(3,2) NULL,
        PrimaryImageUrl NVARCHAR(500) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_Cars_IsActive DEFAULT (1),
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_Cars_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAt DATETIME2(0) NULL,
        CONSTRAINT FK_Cars_Branches FOREIGN KEY (BranchId) REFERENCES rentx.Branches(BranchId),
        CONSTRAINT CK_Cars_CarType CHECK (CarType IN ('hatchback','sedan','suv','luxury')),
        CONSTRAINT CK_Cars_Fuel CHECK (Fuel IN ('petrol','diesel','ev','hybrid')),
        CONSTRAINT CK_Cars_Transmission CHECK (Transmission IN ('manual','automatic')),
        CONSTRAINT CK_Cars_Seats CHECK (Seats BETWEEN 2 AND 8),
        CONSTRAINT CK_Cars_DailyPrice CHECK (DailyPrice >= 300),
        CONSTRAINT CK_Cars_Odometer CHECK (Odometer >= 0)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'rentx.Cars') AND name = N'UX_Cars_RegNo')
BEGIN
    CREATE UNIQUE INDEX UX_Cars_RegNo ON rentx.Cars(RegNo);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'rentx.Cars') AND name = N'IX_Cars_Branch_Active')
BEGIN
    CREATE INDEX IX_Cars_Branch_Active ON rentx.Cars(BranchId, IsActive);
END
GO

IF OBJECT_ID(N'rentx.CarImages', N'U') IS NULL
BEGIN
    CREATE TABLE rentx.CarImages
    (
        CarImageId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CarId VARCHAR(20) NOT NULL,
        ImageUrl NVARCHAR(500) NOT NULL,
        SortOrder INT NOT NULL CONSTRAINT DF_CarImages_SortOrder DEFAULT (0),
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_CarImages_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_CarImages_Cars FOREIGN KEY (CarId) REFERENCES rentx.Cars(CarId)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'rentx.CarImages') AND name = N'IX_CarImages_CarId')
BEGIN
    CREATE INDEX IX_CarImages_CarId ON rentx.CarImages(CarId, SortOrder);
END
GO

IF OBJECT_ID(N'rentx.RolePermissions', N'U') IS NULL
BEGIN
    CREATE TABLE rentx.RolePermissions
    (
        RoleCode VARCHAR(20) NOT NULL,
        ModuleName VARCHAR(40) NOT NULL,
        CanView BIT NOT NULL,
        CanCreate BIT NOT NULL,
        CanEdit BIT NOT NULL,
        CanDelete BIT NOT NULL,
        CanApprove BIT NOT NULL,
        UpdatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_RolePermissions_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_RolePermissions PRIMARY KEY (RoleCode, ModuleName),
        CONSTRAINT FK_RolePermissions_Roles FOREIGN KEY (RoleCode) REFERENCES rentx.Roles(RoleCode)
    );
END
GO

/* =========================
   PHASE 2: CUSTOMERS + BOOKINGS + OPS
   ========================= */

IF OBJECT_ID(N'rentx.Customers', N'U') IS NULL
BEGIN
    CREATE TABLE rentx.Customers
    (
        CustomerId VARCHAR(20) NOT NULL PRIMARY KEY,
        CustomerType VARCHAR(20) NOT NULL CONSTRAINT DF_Customers_Type DEFAULT ('individual'),
        Name NVARCHAR(120) NOT NULL,
        Phone VARCHAR(10) NOT NULL,
        Email NVARCHAR(255) NULL,
        Dob DATE NULL,
        KycType VARCHAR(20) NOT NULL,
        KycNumber NVARCHAR(60) NOT NULL,
        DlNumber NVARCHAR(40) NULL,
        DlExpiry DATE NULL,
        Address NVARCHAR(300) NULL,
        City NVARCHAR(120) NULL,
        State NVARCHAR(50) NULL,
        Pincode CHAR(6) NULL,
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_Customers_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAt DATETIME2(0) NULL,
        CONSTRAINT CK_Customers_KycType CHECK (KycType IN ('aadhaar','passport','pan','dl')),
        CONSTRAINT CK_Customers_Phone CHECK (Phone NOT LIKE '%[^0-9]%'),
        CONSTRAINT CK_Customers_Pincode CHECK (Pincode IS NULL OR Pincode NOT LIKE '%[^0-9]%'),
        CONSTRAINT CK_Customers_DlExpiry CHECK (DlExpiry IS NULL OR DlExpiry > CAST(SYSUTCDATETIME() AS DATE))
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'rentx.Customers') AND name = N'IX_Customers_Name')
BEGIN
    CREATE INDEX IX_Customers_Name ON rentx.Customers(Name);
END
GO

IF OBJECT_ID(N'rentx.Bookings', N'U') IS NULL
BEGIN
    CREATE TABLE rentx.Bookings
    (
        BookingId VARCHAR(20) NOT NULL PRIMARY KEY,
        CustomerId VARCHAR(20) NOT NULL,
        CarId VARCHAR(20) NOT NULL,
        LocationCode VARCHAR(6) NOT NULL,
        PickAt DATETIME2(0) NOT NULL,
        DropAt DATETIME2(0) NOT NULL,
        DailyPrice DECIMAL(10,2) NOT NULL,
        Days INT NOT NULL,
        Status VARCHAR(20) NOT NULL,
        CancelReason NVARCHAR(500) NULL,
        Notes NVARCHAR(500) NULL,
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_Bookings_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAt DATETIME2(0) NULL,
        CONSTRAINT FK_Bookings_Customers FOREIGN KEY (CustomerId) REFERENCES rentx.Customers(CustomerId),
        CONSTRAINT FK_Bookings_Cars FOREIGN KEY (CarId) REFERENCES rentx.Cars(CarId),
        CONSTRAINT FK_Bookings_Branches FOREIGN KEY (LocationCode) REFERENCES rentx.Branches(BranchId),
        CONSTRAINT CK_Bookings_Status CHECK (Status IN ('pending','approved','ongoing','completed','cancelled')),
        CONSTRAINT CK_Bookings_Range CHECK (DropAt > PickAt),
        CONSTRAINT CK_Bookings_Days CHECK (Days > 0),
        CONSTRAINT CK_Bookings_DailyPrice CHECK (DailyPrice >= 0)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'rentx.Bookings') AND name = N'IX_Bookings_PickAt')
BEGIN
    CREATE INDEX IX_Bookings_PickAt ON rentx.Bookings(PickAt DESC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'rentx.Bookings') AND name = N'IX_Bookings_Status')
BEGIN
    CREATE INDEX IX_Bookings_Status ON rentx.Bookings(Status, LocationCode);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'rentx.Bookings') AND name = N'IX_Bookings_Car_Time')
BEGIN
    CREATE INDEX IX_Bookings_Car_Time ON rentx.Bookings(CarId, PickAt, DropAt);
END
GO

IF OBJECT_ID(N'rentx.MaintenanceBlocks', N'U') IS NULL
BEGIN
    CREATE TABLE rentx.MaintenanceBlocks
    (
        MaintenanceId VARCHAR(20) NOT NULL PRIMARY KEY,
        CarId VARCHAR(20) NOT NULL,
        MaintenanceType VARCHAR(20) NOT NULL,
        BlockFrom DATE NOT NULL,
        BlockTo DATE NOT NULL,
        Days INT NOT NULL,
        Notes NVARCHAR(500) NULL,
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_MaintenanceBlocks_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_MaintenanceBlocks_Cars FOREIGN KEY (CarId) REFERENCES rentx.Cars(CarId),
        CONSTRAINT CK_MaintenanceBlocks_Type CHECK (MaintenanceType IN ('service','repair','insurance','puc','other')),
        CONSTRAINT CK_MaintenanceBlocks_Range CHECK (BlockTo >= BlockFrom),
        CONSTRAINT CK_MaintenanceBlocks_Days CHECK (Days > 0)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'rentx.MaintenanceBlocks') AND name = N'IX_MaintenanceBlocks_Car_Date')
BEGIN
    CREATE INDEX IX_MaintenanceBlocks_Car_Date ON rentx.MaintenanceBlocks(CarId, BlockFrom, BlockTo);
END
GO

IF OBJECT_ID(N'rentx.ReturnInspections', N'U') IS NULL
BEGIN
    CREATE TABLE rentx.ReturnInspections
    (
        InspectionId VARCHAR(20) NOT NULL PRIMARY KEY,
        BookingId VARCHAR(20) NOT NULL,
        CarId VARCHAR(20) NOT NULL,
        Odometer INT NOT NULL,
        FuelPercent TINYINT NOT NULL,
        CleaningRequired BIT NOT NULL,
        LateHours INT NOT NULL,
        LateFeePerHour DECIMAL(10,2) NOT NULL,
        Deposit DECIMAL(10,2) NOT NULL,
        Notes NVARCHAR(500) NULL,
        TotalDamage DECIMAL(10,2) NOT NULL,
        FuelCharge DECIMAL(10,2) NOT NULL,
        CleaningCharge DECIMAL(10,2) NOT NULL,
        LateFee DECIMAL(10,2) NOT NULL,
        SubTotal DECIMAL(10,2) NOT NULL,
        NetPayable DECIMAL(10,2) NOT NULL,
        Refund DECIMAL(10,2) NOT NULL,
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_ReturnInspections_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAt DATETIME2(0) NULL,
        CONSTRAINT FK_ReturnInspections_Bookings FOREIGN KEY (BookingId) REFERENCES rentx.Bookings(BookingId),
        CONSTRAINT FK_ReturnInspections_Cars FOREIGN KEY (CarId) REFERENCES rentx.Cars(CarId),
        CONSTRAINT CK_ReturnInspections_Odometer CHECK (Odometer >= 0),
        CONSTRAINT CK_ReturnInspections_FuelPercent CHECK (FuelPercent BETWEEN 0 AND 100),
        CONSTRAINT CK_ReturnInspections_LateHours CHECK (LateHours >= 0),
        CONSTRAINT CK_ReturnInspections_Amounts CHECK (
            LateFeePerHour >= 0 AND Deposit >= 0 AND TotalDamage >= 0 AND
            FuelCharge >= 0 AND CleaningCharge >= 0 AND LateFee >= 0 AND
            SubTotal >= 0 AND NetPayable >= 0 AND Refund >= 0
        )
    );
END
GO

IF OBJECT_ID(N'rentx.ReturnInspectionDamages', N'U') IS NULL
BEGIN
    CREATE TABLE rentx.ReturnInspectionDamages
    (
        DamageId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        InspectionId VARCHAR(20) NOT NULL,
        Part NVARCHAR(120) NOT NULL,
        Severity VARCHAR(20) NOT NULL,
        EstCost DECIMAL(10,2) NOT NULL,
        Notes NVARCHAR(500) NULL,
        CONSTRAINT FK_ReturnInspectionDamages_Inspections FOREIGN KEY (InspectionId) REFERENCES rentx.ReturnInspections(InspectionId),
        CONSTRAINT CK_ReturnInspectionDamages_Severity CHECK (Severity IN ('minor','moderate','major')),
        CONSTRAINT CK_ReturnInspectionDamages_EstCost CHECK (EstCost >= 0)
    );
END
GO

IF OBJECT_ID(N'rentx.ReturnInspectionDamagePhotos', N'U') IS NULL
BEGIN
    CREATE TABLE rentx.ReturnInspectionDamagePhotos
    (
        DamagePhotoId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        DamageId BIGINT NOT NULL,
        PhotoUrl NVARCHAR(500) NOT NULL,
        CONSTRAINT FK_ReturnInspectionDamagePhotos_Damages FOREIGN KEY (DamageId) REFERENCES rentx.ReturnInspectionDamages(DamageId)
    );
END
GO

IF OBJECT_ID(N'rentx.Notifications', N'U') IS NULL
BEGIN
    CREATE TABLE rentx.Notifications
    (
        NotificationId VARCHAR(20) NOT NULL PRIMARY KEY,
        UserId VARCHAR(20) NULL,
        Title NVARCHAR(160) NOT NULL,
        Message NVARCHAR(500) NOT NULL,
        IsRead BIT NOT NULL CONSTRAINT DF_Notifications_IsRead DEFAULT (0),
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_Notifications_CreatedAt DEFAULT (SYSUTCDATETIME()),
        ReadAt DATETIME2(0) NULL,
        CONSTRAINT FK_Notifications_Users FOREIGN KEY (UserId) REFERENCES rentx.Users(UserId)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'rentx.Notifications') AND name = N'IX_Notifications_User_Read')
BEGIN
    CREATE INDEX IX_Notifications_User_Read ON rentx.Notifications(UserId, IsRead, CreatedAt DESC);
END
GO

/* =========================
   SEED DATA
   ========================= */

IF NOT EXISTS (SELECT 1 FROM rentx.Roles WHERE RoleCode = 'admin')
    INSERT INTO rentx.Roles(RoleCode, RoleName) VALUES ('admin', N'Administrator');
IF NOT EXISTS (SELECT 1 FROM rentx.Roles WHERE RoleCode = 'ops')
    INSERT INTO rentx.Roles(RoleCode, RoleName) VALUES ('ops', N'Operations');
IF NOT EXISTS (SELECT 1 FROM rentx.Roles WHERE RoleCode = 'agent')
    INSERT INTO rentx.Roles(RoleCode, RoleName) VALUES ('agent', N'Agent');
IF NOT EXISTS (SELECT 1 FROM rentx.Roles WHERE RoleCode = 'viewer')
    INSERT INTO rentx.Roles(RoleCode, RoleName) VALUES ('viewer', N'Viewer');
GO

MERGE rentx.RolePermissions AS T
USING (
    SELECT * FROM (VALUES
    ('admin','Bookings',1,1,1,1,1),('admin','Cars',1,1,1,1,1),('admin','Customers',1,1,1,1,1),('admin','Branches',1,1,1,1,1),('admin','Maintenance',1,1,1,1,1),('admin','Reports',1,1,1,1,1),
    ('ops','Bookings',1,1,1,0,1),('ops','Cars',1,1,1,0,1),('ops','Customers',1,1,1,0,0),('ops','Branches',1,0,1,0,0),('ops','Maintenance',1,1,1,0,0),('ops','Reports',1,0,0,0,0),
    ('agent','Bookings',1,1,0,0,0),('agent','Cars',1,1,0,0,0),('agent','Customers',1,1,0,0,0),('agent','Branches',1,0,0,0,0),('agent','Maintenance',1,0,0,0,0),('agent','Reports',1,0,0,0,0),
    ('viewer','Bookings',1,0,0,0,0),('viewer','Cars',1,0,0,0,0),('viewer','Customers',1,0,0,0,0),('viewer','Branches',1,0,0,0,0),('viewer','Maintenance',1,0,0,0,0),('viewer','Reports',1,0,0,0,0)
    ) X(RoleCode, ModuleName, CanView, CanCreate, CanEdit, CanDelete, CanApprove)
) AS S
ON (T.RoleCode = S.RoleCode AND T.ModuleName = S.ModuleName)
WHEN MATCHED THEN UPDATE SET
    T.CanView = S.CanView,
    T.CanCreate = S.CanCreate,
    T.CanEdit = S.CanEdit,
    T.CanDelete = S.CanDelete,
    T.CanApprove = S.CanApprove,
    T.UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (RoleCode, ModuleName, CanView, CanCreate, CanEdit, CanDelete, CanApprove)
    VALUES (S.RoleCode, S.ModuleName, S.CanView, S.CanCreate, S.CanEdit, S.CanDelete, S.CanApprove);
GO

IF NOT EXISTS (SELECT 1 FROM rentx.Branches WHERE BranchId = 'PNQ')
BEGIN
    INSERT INTO rentx.Branches
    (BranchId, Name, Phone, Email, Address, City, State, Pincode, OpenAt, CloseAt, IsActive)
    VALUES
    ('PNQ', N'Pune', '020-11112222', 'pune@company.com', N'Airport Road', N'Pune', 'MH', '411001', '09:00', '21:00', 1);
END

IF NOT EXISTS (SELECT 1 FROM rentx.Branches WHERE BranchId = 'BOM')
BEGIN
    INSERT INTO rentx.Branches
    (BranchId, Name, Phone, Email, Address, City, State, Pincode, OpenAt, CloseAt, IsActive)
    VALUES
    ('BOM', N'Mumbai', '022-11112222', 'mumbai@company.com', N'Andheri East', N'Mumbai', 'MH', '400069', '09:00', '21:00', 1);
END

IF NOT EXISTS (SELECT 1 FROM rentx.Branches WHERE BranchId = 'NAG')
BEGIN
    INSERT INTO rentx.Branches
    (BranchId, Name, Phone, Email, Address, City, State, Pincode, OpenAt, CloseAt, IsActive)
    VALUES
    ('NAG', N'Nagpur', '0712-11112222', 'nagpur@company.com', N'Wardha Road', N'Nagpur', 'MH', '440015', '09:00', '21:00', 1);
END
GO

IF NOT EXISTS (SELECT 1 FROM rentx.Users WHERE UserId = 'U-1004')
BEGIN
    INSERT INTO rentx.Users
    (UserId, Username, FullName, Email, Phone, RoleCode, PasswordHash, IsActive)
    VALUES
    ('U-1004', 'admin', N'Admin User', 'admin@demo.com', '9876543210', 'admin', 'admin', 1);
END

IF NOT EXISTS (SELECT 1 FROM rentx.Users WHERE UserId = 'U-1005')
BEGIN
    INSERT INTO rentx.Users
    (UserId, Username, FullName, Email, Phone, RoleCode, PasswordHash, IsActive)
    VALUES
    ('U-1005', 'opslead', N'Ops Lead', 'ops@company.com', '9765432109', 'ops', 'Temp@123', 1);
END
GO

IF NOT EXISTS (SELECT 1 FROM rentx.Cars WHERE CarId = 'CAR-1001')
BEGIN
    INSERT INTO rentx.Cars
    (CarId, Brand, Model, CarType, Fuel, Transmission, Seats, DailyPrice, RegNo, Odometer, BranchId, Rating, PrimaryImageUrl, IsActive)
    VALUES
    ('CAR-1001', N'Honda', N'City', 'sedan', 'petrol', 'automatic', 5, 2700, 'MH12-AB-1234', 42000, 'PNQ', 4.60, 'https://cdn.example.com/cars/city.jpg', 1);
END

IF NOT EXISTS (SELECT 1 FROM rentx.Cars WHERE CarId = 'CAR-1002')
BEGIN
    INSERT INTO rentx.Cars
    (CarId, Brand, Model, CarType, Fuel, Transmission, Seats, DailyPrice, RegNo, Odometer, BranchId, Rating, PrimaryImageUrl, IsActive)
    VALUES
    ('CAR-1002', N'Hyundai', N'Creta', 'suv', 'diesel', 'manual', 5, 3200, 'MH01-CD-8899', 30000, 'BOM', 4.50, 'https://cdn.example.com/cars/creta.jpg', 1);
END

IF NOT EXISTS (SELECT 1 FROM rentx.Cars WHERE CarId = 'CAR-1003')
BEGIN
    INSERT INTO rentx.Cars
    (CarId, Brand, Model, CarType, Fuel, Transmission, Seats, DailyPrice, RegNo, Odometer, BranchId, Rating, PrimaryImageUrl, IsActive)
    VALUES
    ('CAR-1003', N'Tata', N'Nexon EV', 'suv', 'ev', 'automatic', 5, 3500, 'MH31-EV-2233', 12000, 'NAG', 4.70, 'https://cdn.example.com/cars/nexon-ev.jpg', 1);
END
GO

IF NOT EXISTS (SELECT 1 FROM rentx.CarImages WHERE CarId = 'CAR-1001' AND ImageUrl = 'https://cdn.example.com/cars/city-1.jpg')
    INSERT INTO rentx.CarImages(CarId, ImageUrl, SortOrder) VALUES ('CAR-1001', 'https://cdn.example.com/cars/city-1.jpg', 1);
IF NOT EXISTS (SELECT 1 FROM rentx.CarImages WHERE CarId = 'CAR-1002' AND ImageUrl = 'https://cdn.example.com/cars/creta-1.jpg')
    INSERT INTO rentx.CarImages(CarId, ImageUrl, SortOrder) VALUES ('CAR-1002', 'https://cdn.example.com/cars/creta-1.jpg', 1);
IF NOT EXISTS (SELECT 1 FROM rentx.CarImages WHERE CarId = 'CAR-1003' AND ImageUrl = 'https://cdn.example.com/cars/nexon-ev-1.jpg')
    INSERT INTO rentx.CarImages(CarId, ImageUrl, SortOrder) VALUES ('CAR-1003', 'https://cdn.example.com/cars/nexon-ev-1.jpg', 1);
GO

IF NOT EXISTS (SELECT 1 FROM rentx.Customers WHERE CustomerId = 'CUS-1001')
BEGIN
    INSERT INTO rentx.Customers
    (CustomerId, CustomerType, Name, Phone, Email, Dob, KycType, KycNumber, DlNumber, DlExpiry, Address, City, State, Pincode)
    VALUES
    ('CUS-1001', 'individual', N'A. Kulkarni', '9876543210', 'akulkarni@example.com', '1994-08-21', 'aadhaar', '123412341234', 'DL0420110149646', '2028-12-31', N'Koregaon Park', N'Pune', 'MH', '411001');
END

IF NOT EXISTS (SELECT 1 FROM rentx.Customers WHERE CustomerId = 'CUS-1002')
BEGIN
    INSERT INTO rentx.Customers
    (CustomerId, CustomerType, Name, Phone, Email, Dob, KycType, KycNumber, DlNumber, DlExpiry, Address, City, State, Pincode)
    VALUES
    ('CUS-1002', 'individual', N'Rahul Sharma', '9988776655', 'rahul@example.com', '1992-01-10', 'pan', 'ABCDE1234F', 'DL0420151234567', '2029-01-01', N'Baner', N'Pune', 'MH', '411045');
END
GO

IF NOT EXISTS (SELECT 1 FROM rentx.Bookings WHERE BookingId = 'BK-1045')
BEGIN
    INSERT INTO rentx.Bookings
    (BookingId, CustomerId, CarId, LocationCode, PickAt, DropAt, DailyPrice, Days, Status, Notes)
    VALUES
    ('BK-1045', 'CUS-1001', 'CAR-1001', 'PNQ', '2026-03-10T10:00:00Z', '2026-03-12T10:00:00Z', 2700, 2, 'pending', N'Airport pickup');
END

IF NOT EXISTS (SELECT 1 FROM rentx.Bookings WHERE BookingId = 'BK-1046')
BEGIN
    INSERT INTO rentx.Bookings
    (BookingId, CustomerId, CarId, LocationCode, PickAt, DropAt, DailyPrice, Days, Status, Notes)
    VALUES
    ('BK-1046', 'CUS-1002', 'CAR-1002', 'BOM', '2026-03-05T09:00:00Z', '2026-03-07T09:00:00Z', 3200, 2, 'ongoing', N'Corporate client');
END
GO

IF NOT EXISTS (SELECT 1 FROM rentx.MaintenanceBlocks WHERE MaintenanceId = 'MT-1003')
BEGIN
    INSERT INTO rentx.MaintenanceBlocks
    (MaintenanceId, CarId, MaintenanceType, BlockFrom, BlockTo, Days, Notes)
    VALUES
    ('MT-1003', 'CAR-1001', 'service', '2026-03-15', '2026-03-17', 3, N'60k km service');
END
GO

IF NOT EXISTS (SELECT 1 FROM rentx.Notifications WHERE NotificationId = 'NOT-1001')
BEGIN
    INSERT INTO rentx.Notifications (NotificationId, UserId, Title, Message, IsRead)
    VALUES ('NOT-1001', 'U-1004', N'Booking pending approval', N'BK-1045 is waiting for approval', 0);
END

IF NOT EXISTS (SELECT 1 FROM rentx.Notifications WHERE NotificationId = 'NOT-1002')
BEGIN
    INSERT INTO rentx.Notifications (NotificationId, UserId, Title, Message, IsRead)
    VALUES ('NOT-1002', 'U-1004', N'Maintenance due', N'CAR-1001 has a service block this week', 0);
END
GO

PRINT 'RentX DB phase script executed successfully.';
GO
