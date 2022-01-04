﻿using Microsoft.EntityFrameworkCore.Migrations;

namespace MyCourse.Migrations
{
    public partial class CourseAuthor : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Il comando sql PRAGMA foreign_keys = 0; serve a sospendere temporaneamente il controllo sui vincoli di foreign key
            // in modo che possiamo manipolare la tabella Courses senza causare effetti collaterali sulle tabelle dipendenti da essa,
            // come ad esempio la tabella Lessons.
            // Il controllo sui vincoli verrà poi riabilitato con PRAGMA foreign_keys = 1; dopo aver eseguito tutti gli altri comandi sql.

            // IMPORTANTISSIMO: il comando sql PRAGMA non funziona nel contesto di una transazione, perciò bisogna
            // fornire true come secondo parametro al metodo migrationBuilder.Sql per sopprimere la transazione. Ad esempio:
            // migrationBuilder.Sql("QUI COMANDI SQL", suppressTransaction: true);
            migrationBuilder.Sql(@"PRAGMA foreign_keys = 0;

CREATE TABLE sqlitestudio_temp_table AS SELECT *
                                          FROM Courses;

DROP TABLE Courses;

CREATE TABLE Courses (
    Id                    INTEGER NOT NULL
                                  CONSTRAINT PK_Courses PRIMARY KEY AUTOINCREMENT,
    Title                 TEXT,
    Description           TEXT,
    ImagePath             TEXT,
    Author                TEXT,
    Email                 TEXT,
    Rating                REAL    NOT NULL,
    FullPrice_Amount      REAL,
    FullPrice_Currency    TEXT,
    CurrentPrice_Amount   REAL,
    CurrentPrice_Currency TEXT,
    RowVersion            TEXT,
    Status                TEXT    NOT NULL
                                  DEFAULT 'Deleted',
    AuthorId              TEXT    REFERENCES AspNetUsers (Id) 
);

INSERT INTO Courses (
                        Id,
                        Title,
                        Description,
                        ImagePath,
                        Author,
                        Email,
                        Rating,
                        FullPrice_Amount,
                        FullPrice_Currency,
                        CurrentPrice_Amount,
                        CurrentPrice_Currency,
                        RowVersion,
                        Status
                    )
                    SELECT Id,
                           Title,
                           Description,
                           ImagePath,
                           Author,
                           Email,
                           Rating,
                           FullPrice_Amount,
                           FullPrice_Currency,
                           CurrentPrice_Amount,
                           CurrentPrice_Currency,
                           RowVersion,
                           Status
                      FROM sqlitestudio_temp_table;

DROP TABLE sqlitestudio_temp_table;

CREATE UNIQUE INDEX IX_Courses_Title ON Courses (
    'Title'
);

CREATE TRIGGER CoursesSetRowVersionOnInsert
         AFTER INSERT
            ON Courses
BEGIN
    UPDATE Courses
       SET RowVersion = CURRENT_TIMESTAMP
     WHERE Id = NEW.Id;
END;

CREATE TRIGGER CoursesSetRowVersionOnUpdate
         AFTER UPDATE
            ON Courses
          WHEN NEW.RowVersion <= OLD.RowVersion
BEGIN
    UPDATE Courses
       SET RowVersion = CURRENT_TIMESTAMP
     WHERE Id = NEW.Id;
END;

PRAGMA foreign_keys = 1;
", suppressTransaction: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"PRAGMA foreign_keys = 0;

CREATE TABLE sqlitestudio_temp_table AS SELECT *
                                          FROM Courses;

DROP TABLE Courses;

CREATE TABLE Courses (
    Id                    INTEGER NOT NULL
                                  CONSTRAINT PK_Courses PRIMARY KEY AUTOINCREMENT,
    Title                 TEXT,
    Description           TEXT,
    ImagePath             TEXT,
    Author                TEXT,
    Email                 TEXT,
    Rating                REAL    NOT NULL,
    FullPrice_Amount      REAL,
    FullPrice_Currency    TEXT,
    CurrentPrice_Amount   REAL,
    CurrentPrice_Currency TEXT,
    RowVersion            TEXT,
    Status                TEXT    NOT NULL
                                  DEFAULT 'Deleted'
);

INSERT INTO Courses (
                        Id,
                        Title,
                        Description,
                        ImagePath,
                        Author,
                        Email,
                        Rating,
                        FullPrice_Amount,
                        FullPrice_Currency,
                        CurrentPrice_Amount,
                        CurrentPrice_Currency,
                        RowVersion,
                        Status
                    )
                    SELECT Id,
                           Title,
                           Description,
                           ImagePath,
                           Author,
                           Email,
                           Rating,
                           FullPrice_Amount,
                           FullPrice_Currency,
                           CurrentPrice_Amount,
                           CurrentPrice_Currency,
                           RowVersion,
                           Status
                      FROM sqlitestudio_temp_table;

DROP TABLE sqlitestudio_temp_table;

CREATE UNIQUE INDEX IX_Courses_Title ON Courses (
    'Title'
);

CREATE TRIGGER CoursesSetRowVersionOnInsert
         AFTER INSERT
            ON Courses
BEGIN
    UPDATE Courses
       SET RowVersion = CURRENT_TIMESTAMP
     WHERE Id = NEW.Id;
END;

CREATE TRIGGER CoursesSetRowVersionOnUpdate
         AFTER UPDATE
            ON Courses
          WHEN NEW.RowVersion <= OLD.RowVersion
BEGIN
    UPDATE Courses
       SET RowVersion = CURRENT_TIMESTAMP
     WHERE Id = NEW.Id;
END;

PRAGMA foreign_keys = 1;
", suppressTransaction: true);
        }
    }
}
