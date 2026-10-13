/* =========================================================================
   RaceDay Database Script
   Target: Microsoft SQL Server (SSMS)
   Description: Creates the full RaceDay schema (matches docs/ERD.pdf exactly)
                and seeds sample data: 2 Organisers, 2 Participants, 3 Events,
                categories for each event, and sample enrolments.
   Run this on a fresh SQL Server instance from top to bottom.
   ========================================================================= */
USE RaceDay

/* =========================================================================
   1. TABLE CREATION
   ========================================================================= */

-- Roles: lookup table for the two system roles
CREATE TABLE Roles (
    RoleId      INT IDENTITY(1,1) PRIMARY KEY,
    RoleName    VARCHAR(20) NOT NULL UNIQUE
);
GO

-- Users: shared table for both Organisers and Participants, distinguished by RoleId
CREATE TABLE Users (
    UserId          INT IDENTITY(1,1) PRIMARY KEY,
    FullName        VARCHAR(100) NOT NULL,
    Email           VARCHAR(150) NOT NULL UNIQUE,
    PasswordHash    VARCHAR(255) NOT NULL,
    RoleId          INT NOT NULL,
    CreatedAt       DATETIME NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleId) REFERENCES Roles(RoleId)
);
GO

-- Events: created by an Organiser (a User with RoleId = Organiser)
CREATE TABLE Events (
    EventId         INT IDENTITY(1,1) PRIMARY KEY,
    OrganiserId     INT NOT NULL,
    EventName       VARCHAR(150) NOT NULL,
    Description     VARCHAR(500) NULL,
    EventDate       DATE NOT NULL,
    Location        VARCHAR(150) NOT NULL,
    CreatedAt       DATETIME NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_Events_Users FOREIGN KEY (OrganiserId) REFERENCES Users(UserId)
);
GO

-- Categories: each Event has one-to-many Categories (e.g. 5km, 10km, Half Marathon)
CREATE TABLE Categories (
    CategoryId      INT IDENTITY(1,1) PRIMARY KEY,
    EventId         INT NOT NULL,
    CategoryName    VARCHAR(100) NOT NULL,
    DistanceKm      DECIMAL(5,2) NOT NULL,
    MaxParticipants INT NOT NULL DEFAULT 100,
    EntryFee        DECIMAL(8,2) NOT NULL DEFAULT 0,
    CONSTRAINT FK_Categories_Events FOREIGN KEY (EventId) REFERENCES Events(EventId)
);
GO

-- Enrolments: junction table implementing the many-to-many relationship
-- between Participants (Users) and Categories, with enrolment-specific attributes
CREATE TABLE Enrolments (
    EnrolmentId     INT IDENTITY(1,1) PRIMARY KEY,
    ParticipantId   INT NOT NULL,
    CategoryId      INT NOT NULL,
    EnrolmentDate   DATETIME NOT NULL DEFAULT GETDATE(),
    Status          VARCHAR(20) NOT NULL DEFAULT 'Registered',
    CONSTRAINT FK_Enrolments_Users FOREIGN KEY (ParticipantId) REFERENCES Users(UserId),
    CONSTRAINT FK_Enrolments_Categories FOREIGN KEY (CategoryId) REFERENCES Categories(CategoryId),
    CONSTRAINT UQ_Enrolments_Participant_Category UNIQUE (ParticipantId, CategoryId)
);
GO

-- Results: one-to-one with Enrolments (each enrolment has at most one result)
CREATE TABLE Results (
    ResultId        INT IDENTITY(1,1) PRIMARY KEY,
    EnrolmentId     INT NOT NULL UNIQUE,
    FinishTime      TIME NOT NULL,
    Position        INT NULL,
    RecordedAt      DATETIME NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_Results_Enrolments FOREIGN KEY (EnrolmentId) REFERENCES Enrolments(EnrolmentId)
);
GO

/* =========================================================================
   2. SEED DATA
   ========================================================================= */

-- Roles
INSERT INTO Roles (RoleName) VALUES ('Organiser'), ('Participant');
GO

-- 2 Organisers + 2 Participants
INSERT INTO Users (FullName, Email, PasswordHash, RoleId) VALUES
('Thandeka Mkhize',    'thandeka.mkhize@raceday.co.za',  'HASHED_PWD_1', (SELECT RoleId FROM Roles WHERE RoleName = 'Organiser')),
('Johan van der Merwe','johan.vdm@raceday.co.za',        'HASHED_PWD_2', (SELECT RoleId FROM Roles WHERE RoleName = 'Organiser')),
('Somila Ngxishe',     'somila.ngxishe@raceday.co.za',   'HASHED_PWD_3', (SELECT RoleId FROM Roles WHERE RoleName = 'Participant')),
('Aiden Petersen',     'aiden.petersen@raceday.co.za',   'HASHED_PWD_4', (SELECT RoleId FROM Roles WHERE RoleName = 'Participant'));
GO

-- 3 Events (owned by the 2 organisers)
INSERT INTO Events (OrganiserId, EventName, Description, EventDate, Location) VALUES
((SELECT UserId FROM Users WHERE Email = 'thandeka.mkhize@raceday.co.za'), 'Gqeberha Bay Marathon', 'Annual coastal marathon along the Gqeberha beachfront.', '2026-10-04', 'Gqeberha, Eastern Cape'),
((SELECT UserId FROM Users WHERE Email = 'thandeka.mkhize@raceday.co.za'), 'Despatch Fun Run', 'Community fun run supporting local schools.', '2026-09-12', 'Despatch, Uitenhage'),
((SELECT UserId FROM Users WHERE Email = 'johan.vdm@raceday.co.za'),      'Baviaanskloof Trail Challenge', 'Off-road trail running event through the Baviaanskloof reserve.', '2026-11-21', 'Baviaanskloof, Eastern Cape');
GO

-- Categories for each event
INSERT INTO Categories (EventId, CategoryName, DistanceKm, MaxParticipants, EntryFee) VALUES
((SELECT EventId FROM Events WHERE EventName = 'Gqeberha Bay Marathon'),  'Full Marathon', 42.20, 500, 250.00),
((SELECT EventId FROM Events WHERE EventName = 'Gqeberha Bay Marathon'),  'Half Marathon', 21.10, 500, 180.00),
((SELECT EventId FROM Events WHERE EventName = 'Despatch Fun Run'),       '5km Fun Run',    5.00, 300,  60.00),
((SELECT EventId FROM Events WHERE EventName = 'Despatch Fun Run'),       '10km Run',      10.00, 300,  90.00),
((SELECT EventId FROM Events WHERE EventName = 'Baviaanskloof Trail Challenge'), '15km Trail', 15.00, 150, 150.00),
((SELECT EventId FROM Events WHERE EventName = 'Baviaanskloof Trail Challenge'), '30km Trail', 30.00, 150, 220.00);
GO

-- Sample enrolments (the 2 participants entering a few categories)
INSERT INTO Enrolments (ParticipantId, CategoryId, Status) VALUES
((SELECT UserId FROM Users WHERE Email = 'somila.ngxishe@raceday.co.za'),
 (SELECT CategoryId FROM Categories WHERE CategoryName = 'Half Marathon'), 'Registered'),
((SELECT UserId FROM Users WHERE Email = 'somila.ngxishe@raceday.co.za'),
 (SELECT CategoryId FROM Categories WHERE CategoryName = '5km Fun Run'), 'Registered'),
((SELECT UserId FROM Users WHERE Email = 'aiden.petersen@raceday.co.za'),
 (SELECT CategoryId FROM Categories WHERE CategoryName = '10km Run'), 'Registered'),
((SELECT UserId FROM Users WHERE Email = 'aiden.petersen@raceday.co.za'),
 (SELECT CategoryId FROM Categories WHERE CategoryName = '15km Trail'), 'Registered');
GO

-- Sample result for one completed enrolment (demonstrates the 1-to-1 relationship)
INSERT INTO Results (EnrolmentId, FinishTime, Position) VALUES
((SELECT EnrolmentId FROM Enrolments e
  JOIN Users u ON e.ParticipantId = u.UserId
  JOIN Categories c ON e.CategoryId = c.CategoryId
  WHERE u.Email = 'somila.ngxishe@raceday.co.za' AND c.CategoryName = '5km Fun Run'),
 '00:24:37', 3);
GO

-- DISPLAY ALL THE TABLES OF THE RESULTS
   SELECT * FROM Roles;
   SELECT * FROM Users;
   SELECT * FROM Events;
   SELECT * FROM Categories;
   SELECT * FROM Enrolments;
   SELECT * FROM Results;
 
