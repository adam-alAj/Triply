SELECT
    DestinationId,
    Name,
    COUNT(*) AS DuplicateCount
FROM Places
GROUP BY DestinationId, Name
HAVING COUNT(*) > 1;