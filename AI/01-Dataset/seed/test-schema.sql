-- Test-only schema matching the Backend EF Core InitialCreate + seed
-- migrations (reference tables, Destinations, Places). Used to verify
-- seed_places.py against a faithful copy of the real database shape.
CREATE TABLE Countries (
    Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Countries PRIMARY KEY,
    Name NVARCHAR(450) NOT NULL,
    IsoCode NVARCHAR(450) NOT NULL
);
CREATE UNIQUE INDEX IX_Countries_Name ON Countries (Name);
CREATE UNIQUE INDEX IX_Countries_IsoCode ON Countries (IsoCode);

CREATE TABLE Currencies (
    Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Currencies PRIMARY KEY,
    IsoCode NVARCHAR(450) NOT NULL,
    Symbol NVARCHAR(MAX) NOT NULL
);
CREATE UNIQUE INDEX IX_Currencies_IsoCode ON Currencies (IsoCode);

CREATE TABLE PlaceCategories (
    Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PlaceCategories PRIMARY KEY,
    Code NVARCHAR(450) NOT NULL,
    Label NVARCHAR(MAX) NOT NULL
);
CREATE UNIQUE INDEX IX_PlaceCategories_Code ON PlaceCategories (Code);

CREATE TABLE CostCategories (
    Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CostCategories PRIMARY KEY,
    Code NVARCHAR(450) NOT NULL,
    Label NVARCHAR(MAX) NOT NULL
);
CREATE UNIQUE INDEX IX_CostCategories_Code ON CostCategories (Code);

CREATE TABLE Destinations (
    Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Destinations PRIMARY KEY,
    CountryId BIGINT NOT NULL,
    Name NVARCHAR(450) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    Latitude DECIMAL(10,2) NULL,
    Longitude DECIMAL(10,2) NULL,
    IsSupported BIT NOT NULL DEFAULT 1,
    CONSTRAINT FK_Destinations_Countries FOREIGN KEY (CountryId)
        REFERENCES Countries (Id),
    CONSTRAINT IX_Destinations_CountryId_Name UNIQUE (CountryId, Name)
);

CREATE TABLE Places (
    Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Places PRIMARY KEY,
    DestinationId BIGINT NOT NULL,
    PlaceCategoryId BIGINT NOT NULL,
    Name NVARCHAR(MAX) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    ReferencePrice DECIMAL(10,2) NOT NULL,
    CurrencyId BIGINT NOT NULL,
    CostCategoryId BIGINT NOT NULL,
    PriceUpdatedAt DATETIME2 NOT NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CONSTRAINT FK_Places_Destinations FOREIGN KEY (DestinationId)
        REFERENCES Destinations (Id),
    CONSTRAINT FK_Places_PlaceCategories FOREIGN KEY (PlaceCategoryId)
        REFERENCES PlaceCategories (Id),
    CONSTRAINT FK_Places_Currencies FOREIGN KEY (CurrencyId)
        REFERENCES Currencies (Id),
    CONSTRAINT FK_Places_CostCategories FOREIGN KEY (CostCategoryId)
        REFERENCES CostCategories (Id)
);
CREATE INDEX IX_Places_DestinationId ON Places (DestinationId);
