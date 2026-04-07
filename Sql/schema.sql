CREATE TABLE Users (
    UserId INT IDENTITY(1,1) PRIMARY KEY,
    Username VARCHAR(50) NOT NULL UNIQUE,
    Email VARCHAR(150) NOT NULL,
    PasswordHash VARCHAR(256) NOT NULL,
    PasswordSalt VARCHAR(128) NOT NULL,
    Role VARCHAR(20) NOT NULL,
    IsActive BIT NOT NULL DEFAULT 1
);

CREATE TABLE Regions (
    RegionId INT IDENTITY(1,1) PRIMARY KEY,
    Name VARCHAR(100) NOT NULL UNIQUE,
    IsActive BIT NOT NULL DEFAULT 1
);

CREATE TABLE UserRegions (
    UserId INT NOT NULL,
    RegionId INT NOT NULL,
    PRIMARY KEY (UserId, RegionId),
    FOREIGN KEY (UserId) REFERENCES Users(UserId),
    FOREIGN KEY (RegionId) REFERENCES Regions(RegionId)
);

CREATE TABLE RegionUrls (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    RegionId INT NOT NULL,
    Url VARCHAR(500) NOT NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    FOREIGN KEY (RegionId) REFERENCES Regions(RegionId)
);

CREATE TABLE UserLoginOtp (
    LoginOtpId INT IDENTITY(1,1) PRIMARY KEY,
    UserId INT NOT NULL,
    OtpHash VARCHAR(256) NOT NULL,
    OtpSalt VARCHAR(128) NOT NULL,
    ExpiresOnUtc DATETIME NOT NULL,
    IsUsed BIT NOT NULL DEFAULT 0,
    CreatedOnUtc DATETIME NOT NULL,
    FOREIGN KEY (UserId) REFERENCES Users(UserId)
);

CREATE TABLE PasswordResetTokens (
    PasswordResetTokenId INT IDENTITY(1,1) PRIMARY KEY,
    UserId INT NOT NULL,
    TokenHash VARCHAR(256) NOT NULL,
    TokenSalt VARCHAR(128) NOT NULL,
    ExpiresOnUtc DATETIME NOT NULL,
    IsUsed BIT NOT NULL DEFAULT 0,
    CreatedOnUtc DATETIME NOT NULL,
    FOREIGN KEY (UserId) REFERENCES Users(UserId)
);

CREATE TABLE UserActivityLogs (
    ActivityLogId INT IDENTITY(1,1) PRIMARY KEY,
    UserId INT NOT NULL,
    Username VARCHAR(50) NOT NULL,
    SessionKey VARCHAR(64) NOT NULL,
    ActionName VARCHAR(100) NOT NULL,
    Details VARCHAR(2000) NULL,
    IpAddress VARCHAR(64) NULL,
    LocationText VARCHAR(256) NULL,
    UserAgent VARCHAR(512) NULL,
    CreatedOnUtc DATETIME NOT NULL,
    FOREIGN KEY (UserId) REFERENCES Users(UserId)
);

CREATE INDEX IX_UserActivityLogs_UserDate ON UserActivityLogs(UserId, CreatedOnUtc DESC);
CREATE INDEX IX_UserLoginOtp_UserDate ON UserLoginOtp(UserId, CreatedOnUtc DESC);
CREATE INDEX IX_PasswordResetTokens_UserDate ON PasswordResetTokens(UserId, CreatedOnUtc DESC);
