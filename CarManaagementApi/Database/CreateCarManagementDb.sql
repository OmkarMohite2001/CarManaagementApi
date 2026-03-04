IF DB_ID(N'CarManagementDb') IS NULL
BEGIN
    CREATE DATABASE CarManagementDb;
END
GO

USE CarManagementDb;
GO

IF OBJECT_ID(N'dbo.Cars', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Cars
    (
        CarId INT IDENTITY(1,1) PRIMARY KEY,
        Brand NVARCHAR(100) NOT NULL,
        Model NVARCHAR(100) NOT NULL,
        Variant NVARCHAR(100) NULL,
        RegistrationNumber NVARCHAR(20) NOT NULL UNIQUE,
        ManufactureYear SMALLINT NOT NULL CHECK (ManufactureYear BETWEEN 1980 AND YEAR(GETDATE()) + 1),
        FuelType NVARCHAR(20) NOT NULL CHECK (FuelType IN ('Petrol','Diesel','CNG','EV','Hybrid')),
        Transmission NVARCHAR(20) NOT NULL CHECK (Transmission IN ('Manual','Automatic')),
        Price DECIMAL(18,2) NOT NULL CHECK (Price >= 0),
        IsAvailable BIT NOT NULL CONSTRAINT DF_Cars_IsAvailable DEFAULT (1),
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Cars_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAt DATETIME2 NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Cars_Brand_Model' AND object_id = OBJECT_ID(N'dbo.Cars'))
BEGIN
    CREATE INDEX IX_Cars_Brand_Model ON dbo.Cars(Brand, Model);
END
GO
