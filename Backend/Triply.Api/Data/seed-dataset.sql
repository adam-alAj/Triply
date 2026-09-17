USE TriplyDb;
GO

-- Places
IF NOT EXISTS (SELECT 1 FROM dbo.Places WHERE DestinationId = 1 AND Name = 'Old City of Jerusalem')
BEGIN
    INSERT INTO dbo.Places
    (
        DestinationId,
        PlaceCategoryId,
        Name,
        Description,
        ReferencePrice,
        CurrencyId,
        CostCategoryId,
        PriceUpdatedAt,
        IsActive
    )
    VALUES
    (
        1,
        1,
        'Old City of Jerusalem',
        'Historic old city area',
        0,
        1,
        4,
        SYSUTCDATETIME(),
        1
    );
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Places WHERE DestinationId = 1 AND Name = 'Western Wall')
BEGIN
    INSERT INTO dbo.Places
    (
        DestinationId,
        PlaceCategoryId,
        Name,
        Description,
        ReferencePrice,
        CurrencyId,
        CostCategoryId,
        PriceUpdatedAt,
        IsActive
    )
    VALUES
    (
        1,
        1,
        'Western Wall',
        'Historic religious site',
        0,
        1,
        4,
        SYSUTCDATETIME(),
        1
    );
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Places WHERE DestinationId = 1 AND Name = 'Mahane Yehuda Market')
BEGIN
    INSERT INTO dbo.Places
    (
        DestinationId,
        PlaceCategoryId,
        Name,
        Description,
        ReferencePrice,
        CurrencyId,
        CostCategoryId,
        PriceUpdatedAt,
        IsActive
    )
    VALUES
    (
        1,
        2,
        'Mahane Yehuda Market',
        'Popular food market',
        10,
        1,
        3,
        SYSUTCDATETIME(),
        1
    );
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Places WHERE DestinationId = 2 AND Name = 'Roman Theatre')
BEGIN
    INSERT INTO dbo.Places
    (
        DestinationId,
        PlaceCategoryId,
        Name,
        Description,
        ReferencePrice,
        CurrencyId,
        CostCategoryId,
        PriceUpdatedAt,
        IsActive
    )
    VALUES
    (
        2,
        1,
        'Roman Theatre',
        'Historic Roman theatre in Amman',
        2,
        2,
        4,
        SYSUTCDATETIME(),
        1
    );
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Places WHERE DestinationId = 2 AND Name = 'Rainbow Street')
BEGIN
    INSERT INTO dbo.Places
    (
        DestinationId,
        PlaceCategoryId,
        Name,
        Description,
        ReferencePrice,
        CurrencyId,
        CostCategoryId,
        PriceUpdatedAt,
        IsActive
    )
    VALUES
    (
        2,
        1,
        'Rainbow Street',
        'Popular street with restaurants and shops',
        0,
        2,
        3,
        SYSUTCDATETIME(),
        1
    );
END;

-- Place ↔ Interest mappings

INSERT INTO dbo.PlaceInterests (PlaceId, InterestCategoryId)
SELECT p.Id, 2
FROM dbo.Places p
WHERE p.Name = 'Old City of Jerusalem'
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.PlaceInterests pi
      WHERE pi.PlaceId = p.Id
        AND pi.InterestCategoryId = 2
  );

INSERT INTO dbo.PlaceInterests (PlaceId, InterestCategoryId)
SELECT p.Id, 2
FROM dbo.Places p
WHERE p.Name = 'Western Wall'
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.PlaceInterests pi
      WHERE pi.PlaceId = p.Id
        AND pi.InterestCategoryId = 2
  );

INSERT INTO dbo.PlaceInterests (PlaceId, InterestCategoryId)
SELECT p.Id, 3
FROM dbo.Places p
WHERE p.Name = 'Mahane Yehuda Market'
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.PlaceInterests pi
      WHERE pi.PlaceId = p.Id
        AND pi.InterestCategoryId = 3
  );

INSERT INTO dbo.PlaceInterests (PlaceId, InterestCategoryId)
SELECT p.Id, 2
FROM dbo.Places p
WHERE p.Name = 'Roman Theatre'
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.PlaceInterests pi
      WHERE pi.PlaceId = p.Id
        AND pi.InterestCategoryId = 2
  );

INSERT INTO dbo.PlaceInterests (PlaceId, InterestCategoryId)
SELECT p.Id, 3
FROM dbo.Places p
WHERE p.Name = 'Rainbow Street'
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.PlaceInterests pi
      WHERE pi.PlaceId = p.Id
        AND pi.InterestCategoryId = 3
  );

GO