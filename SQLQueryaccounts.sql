    CREATE TABLE Users (
        UserID INT PRIMARY KEY IDENTITY(1,1), 
        Username VARCHAR(50) UNIQUE NOT NULL,
        Email VARCHAR(100) UNIQUE NOT NULL,
        PasswordHash VARCHAR(255) NOT NULL,
        CreationDate DATETIME DEFAULT GETDATE() 
    );
 


  

    CREATE TABLE games(
    GameID INT PRIMARY KEY IDENTITY(1,1), 
     GameCreationDate DATETIME DEFAULT GETDATE(), 
     GameName VARCHAR(255) NOT NULL, 
     PlayersViews INT );

     SELECT * FROM games

    SELECT * FROM Users