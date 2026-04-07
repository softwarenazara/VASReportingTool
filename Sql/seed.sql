INSERT INTO Regions (Name, IsActive) VALUES
('Africa', 1),
('MENA', 1),
('APAC', 1);

INSERT INTO Users (Username, Email, PasswordHash, PasswordSalt, Role, IsActive) VALUES
('admin', 'samadhan.jadhav@nazara.com', 'Ruk0qBSUF0azwtk9NrEgDF+RYcnXL4v9ACkrQqRHf/E=', 'kY3URrF/SBv/+4lA3I2Nww==', 'Admin', 1),
('report.user', 'samadhan.jadhav@nazara.com', '38ReXg2o1n2vTySQtOmmuvUR4O4Kmp0Gw5G8wA96j4w=', 'MVLiz3npLK7Lro7rMIfd6g==', 'User', 1);

INSERT INTO UserRegions (UserId, RegionId) VALUES
(1, 1), (1, 2), (1, 3),
(2, 1);

INSERT INTO RegionUrls (RegionId, Url, IsActive) VALUES
(1, 'https://africa-backend.example/api', 1),
(2, 'https://mena-backend.example/api', 1),
(3, 'https://apac-backend.example/api', 1);
