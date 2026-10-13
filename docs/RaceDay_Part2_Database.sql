/*
 RaceDay Part 2 schema reference aligned with the EF Core Code-First models.
 The original Part 1 SQL script is preserved in RaceDay_Database.sql.
 Part 2 requires Events.DistanceKm, Events.EventType and Enrolments.EventId.
 Prefer applying EF Core migrations for normal setup; use this script only on a fresh database.
*/
IF DB_ID(N'RaceDayDb') IS NULL CREATE DATABASE RaceDayDb;
GO
USE RaceDayDb;
GO
CREATE TABLE dbo.Roles (
  RoleId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Roles PRIMARY KEY,
  RoleName NVARCHAR(20) NOT NULL CONSTRAINT UQ_Roles_RoleName UNIQUE
);
GO
CREATE TABLE dbo.Users (
  UserId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
  FullName NVARCHAR(100) NOT NULL,
  Email NVARCHAR(150) NOT NULL CONSTRAINT UQ_Users_Email UNIQUE,
  PasswordHash NVARCHAR(255) NOT NULL,
  RoleId INT NOT NULL,
  CreatedAt DATETIME2 NOT NULL,
  CONSTRAINT FK_Users_Roles_RoleId FOREIGN KEY (RoleId) REFERENCES dbo.Roles(RoleId) ON DELETE NO ACTION
);
GO
CREATE TABLE dbo.Events (
  EventId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Events PRIMARY KEY,
  OrganiserId INT NOT NULL,
  EventName NVARCHAR(150) NOT NULL,
  Description NVARCHAR(500) NULL,
  EventDate DATETIME2 NOT NULL,
  Location NVARCHAR(150) NOT NULL,
  DistanceKm DECIMAL(5,2) NOT NULL,
  EventType NVARCHAR(20) NOT NULL,
  CreatedAt DATETIME2 NOT NULL,
  CONSTRAINT FK_Events_Users_OrganiserId FOREIGN KEY (OrganiserId) REFERENCES dbo.Users(UserId) ON DELETE NO ACTION
);
GO
CREATE TABLE dbo.Categories (
  CategoryId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Categories PRIMARY KEY,
  EventId INT NOT NULL,
  CategoryName NVARCHAR(100) NOT NULL,
  DistanceKm DECIMAL(5,2) NOT NULL,
  MaxParticipants INT NOT NULL,
  EntryFee DECIMAL(8,2) NOT NULL,
  CONSTRAINT FK_Categories_Events_EventId FOREIGN KEY (EventId) REFERENCES dbo.Events(EventId) ON DELETE NO ACTION
);
GO
CREATE TABLE dbo.Enrolments (
  EnrolmentId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Enrolments PRIMARY KEY,
  ParticipantId INT NOT NULL,
  EventId INT NOT NULL,
  CategoryId INT NOT NULL,
  EnrolmentDate DATETIME2 NOT NULL,
  Status NVARCHAR(20) NOT NULL,
  CONSTRAINT FK_Enrolments_Users_ParticipantId FOREIGN KEY (ParticipantId) REFERENCES dbo.Users(UserId) ON DELETE NO ACTION,
  CONSTRAINT FK_Enrolments_Events_EventId FOREIGN KEY (EventId) REFERENCES dbo.Events(EventId) ON DELETE NO ACTION,
  CONSTRAINT FK_Enrolments_Categories_CategoryId FOREIGN KEY (CategoryId) REFERENCES dbo.Categories(CategoryId) ON DELETE NO ACTION,
  CONSTRAINT UQ_Enrolments_Participant_Category UNIQUE (ParticipantId, CategoryId)
);
GO
CREATE TABLE dbo.Results (
  ResultId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Results PRIMARY KEY,
  EnrolmentId INT NOT NULL CONSTRAINT UQ_Results_Enrolment UNIQUE,
  FinishTime TIME NOT NULL,
  Position INT NULL,
  RecordedAt DATETIME2 NOT NULL,
  CONSTRAINT FK_Results_Enrolments_EnrolmentId FOREIGN KEY (EnrolmentId) REFERENCES dbo.Enrolments(EnrolmentId) ON DELETE NO ACTION
);
GO
INSERT INTO dbo.Roles (RoleId, RoleName) VALUES (1, N'Organiser'), (2, N'Participant');
GO
