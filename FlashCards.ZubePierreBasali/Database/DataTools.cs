using FlashCards.FlashCardsManager.Models;
using Spectre.Console;
using Dapper;
using System.Data;
using Microsoft.Data.SqlClient;
using FlashCards.StudySessions;
using System.Configuration;
using FlashCards.FlashCardsManager;

namespace FlashCards.Database
{
    internal class DataTools
    {
        internal string? ConnectionString { get; set;}
        internal string? SqlCommandText { get; set; }

        internal void Initialization()
        {

            try
            {
                Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                try
                {
                    // First attempted method to access my local data source, seems the syntax varies depending on the computer location(here Switzerland)
                    // ConnectionString = @$"Data Source=localhost;Integrated Security=SSPI;Initial Catalog=;TrustServerCertificate=True;";
                    ConnectionString = @"Data Source=.;Integrated Security=SSPI;Initial Catalog=;TrustServerCertificate=True;";
                    if (config.AppSettings.Settings["DataBaseName"].Value == "")
                    {
                        config.AppSettings.Settings["DataBaseName"].Value = UserInputs.GetInputString("Choose a name for your data base:");
                        config.Save();
                        using (SqlConnection connection = new(ConnectionString))
                        {
                            if (connection.State == ConnectionState.Closed) connection.Open();
                            SqlCommandText = $@"CREATE DATABASE {config.AppSettings.Settings["DataBaseName"].Value}";
                            SqlCommand command = connection.CreateCommand();
                            command.CommandText = SqlCommandText;
                            command.ExecuteNonQuery();
                            connection.Close();
                        }
                    }
                
                }
                catch { }

                try
                {
                    // same as above
                    // ConnectionString = $"Data Source=localhost;Initial Catalog={config.AppSettings.Settings["DataBaseName"].Value};Integrated Security=True;TrustServerCertificate=True;";

                    ConnectionString = @$"Data Source=.;Initial Catalog={config.AppSettings.Settings["DataBaseName"].Value};Integrated Security=True;TrustServerCertificate=True;";
                    using (SqlConnection connection = new(ConnectionString))
                    {
                        connection.Open();
                        SqlCommand command = connection.CreateCommand();
                        SqlCommandText = @"IF OBJECT_ID(N'dbo.Stacks', N'U') IS NULL CREATE TABLE Stacks (Id INTEGER PRIMARY KEY,NumberOfCards INTEGER,Name nvarchar(20));

                                       IF OBJECT_ID(N'dbo.FlashCards', N'U') IS NULL CREATE TABLE FlashCards (StackId INTEGER,Stack nvarchar(20),Id INTEGER, Question TEXT,Answer TEXT);
                                       
                                       IF NOT (EXISTS(SELECT * FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS WHERE CONSTRAINT_NAME = 'FK_FlashCards_Stacks')) ALTER TABLE [dbo].[FlashCards]
                                       WITH CHECK ADD CONSTRAINT [FK_FlashCards_Stacks] FOREIGN KEY ([StackId])
                                       REFERENCES [dbo].[Stacks] ([Id]) ON UPDATE CASCADE ON DELETE CASCADE;
                                       
                                       IF OBJECT_ID(N'dbo.StudySessions', N'U') IS NULL CREATE TABLE StudySessions (StackId INTEGER,Stack nvarchar(20),Date DATE,QuestionMode TEXT,QuestionCount INTEGER,Score INTEGER,Time TIME);
                                       
                                       IF NOT (EXISTS(SELECT * FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS WHERE CONSTRAINT_NAME = 'FK_StudySessions_Stacks')) ALTER TABLE [dbo].[StudySessions]
                                       WITH CHECK ADD CONSTRAINT [FK_StudySessions_Stacks] FOREIGN KEY ([StackId])
                                       REFERENCES [dbo].[Stacks] ([Id]) ON UPDATE CASCADE ON DELETE CASCADE;";
                        command.CommandText = SqlCommandText;
                        command.ExecuteNonQuery();
                        connection.Close();
                    }
                }
                catch (Exception ex) { throw ex; }
            }
            catch (Exception ex)
            {
                Console.ReadKey();
            }
        }

        internal void ExecuteQuery(string sqlCommand,string stack = "",int stackId = 0, string question = "",string answer = "",int numberOfCards = 0,int id = 0,string date = "",string time = "",string questionMode = "",int? questionCount = 0, int score = 0)
        {
            try
            {
                using (SqlConnection connection = new(ConnectionString))
                {
                    DynamicParameters param = new();
                    param.Add("@stackId", stackId);
                    param.Add("@stack", stack);
                    param.Add("@question", question);
                    param.Add("@answer", answer);
                    param.Add("@numberOfCards", numberOfCards);
                    param.Add("@id", id);
                    param.Add("@date", date);
                    param.Add("@time", time);
                    param.Add("@questionMode", questionMode);
                    param.Add("@questionCount", questionCount);
                    param.Add("@score", score);
                    connection.Query(sqlCommand,param);
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine(ex.Message);
                Console.Read();
            }
        }

        internal void DeleteCard(FlashCard card,string stack)
        {
            SqlCommandText = @$"DELETE FROM FlashCards WHERE Id = @id AND Stack = @stack;
                                UPDATE FlashCards SET Id = Id - 1 WHERE Id > @id AND Stack = @stack";
            ExecuteQuery(SqlCommandText, stack: stack, id:card.Id);
            UpdateStack(stack);
        }

        internal void DeleteStack(Stacks stack)
        {
            SqlCommandText = $"DELETE FROM Stacks WHERE Name = @stack";
            ExecuteQuery(SqlCommandText, stack:stack.Name);
            SqlCommandText = "UPDATE Stacks Set Id = Id - 1 WHERE Id > @id;";
            ExecuteQuery(SqlCommandText, id:stack.Id);
        }

        internal List<FlashCard> GetFlashCards(string stack)
        {
            using (SqlConnection connection = new(ConnectionString)) 
            {
                DynamicParameters param = new();
                param.Add("@stack", stack);
                SqlCommandText = $"SELECT Id,* FROM FlashCards WHERE Stack = @stack ORDER BY FlashCards.Id";
                return connection.Query<FlashCard>(SqlCommandText, param ).ToList();
            }
        }

        internal List<Stacks> GetStacks()
        {

            using (SqlConnection connection = new(ConnectionString))
            {
                SqlCommandText = $"SELECT Id,* FROM Stacks";
                List<Stacks> stacks = connection.Query<Stacks>(SqlCommandText).ToList();
                return stacks;
            } 
        }

        internal void UpdateCard(FlashCard card,string option, string value,string stack)
        {
            switch (option)
            {
                case "Question":
                    SqlCommandText = "UPDATE FlashCards SET Question = @question WHERE Id = @id";
                    ExecuteQuery(SqlCommandText, stack:stack, question: value,id:card.Id);
                    UpdateStack(stack);
                    break;
                case "Answer":
                    SqlCommandText = "UPDATE FlashCards SET Answer = @answer WHERE Id = @id and Stack = @stack";
                    ExecuteQuery(SqlCommandText, stack: stack, answer: value,id:card.Id);
                    UpdateStack(stack);
                    break;
                case "Stack":
                    SqlCommandText = @"UPDATE FlashCards SET Stack = @answer,
                                 Id = (SELECT ISNULL(MAX(Id)+1,1) FROM FlashCards WHERE Stack = @answer)
                                 WHERE Id = @id AND Stack = @stack;
                                 UPDATE FlashCards Set Id = Id - 1 WHERE Id > @id AND Stack = @stack";
                    ExecuteQuery(SqlCommandText, stack: stack, answer:value,id:card.Id);
                    UpdateStack(stack);
                    break;
            }
        }

        internal void UpdateStack(string stack)
        {
            SqlCommandText = @$"UPDATE Stacks SET NumberOfCards = (SELECT Count(Stack) FROM FlashCards WHERE Stack = @stack)
                                WHERE Name = @stack;";
            ExecuteQuery(SqlCommandText, stack:stack);
        }

        internal void AddNewStack(Stacks stack)
        {
            try
            {
                SqlCommandText = @$"INSERT INTO Stacks (Name,NumberOfCards,Id) VALUES(@stack,@numberOfCards,
                                    (SELECT ISNULL(MAX(Id)+1,1) FROM Stacks))";
                ExecuteQuery(SqlCommandText, stack.Name, numberOfCards: stack.NumberOfCards);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"The Stack '{stack.Name}' already exists");
            }
        }

        internal void AddCard(FlashCard card,string stack)
        {
            SqlCommandText = @$"INSERT INTO FlashCards (Question,Answer,Stack,Id) 
                                VALUES(@question,@answer,@stack,
                                (SELECT ISNULL(MAX(Id)+1,1) FROM FlashCards WHERE Stack = @stack))";
            ExecuteQuery(SqlCommandText, question: card.Question, answer: card.Answer, stack: stack);
            UpdateStack(stack);
        }

        internal void AddStudySession(StudyModel studySession)
        {
            SqlCommandText = @$"INSERT INTO StudySessions (Date,Stack,QuestionMode,QuestionCount,Score,Time)
                                VALUES(@date,@stack,@questionMode,@questionCount,@score,@time)";
            ExecuteQuery(SqlCommandText, date: studySession.Date, stack: studySession.Stack,questionMode:studySession.QuestionMode, questionCount: studySession.QuestionCount, score: studySession.Score,time: studySession.Time);
        }

        internal List<StudyModel> GetAllStudySessions()
        {
            using (SqlConnection connection = new(ConnectionString))
            {
                SqlCommandText = "SELECT Stack,Date,QuestionMode,QuestionCount,Score,(FORMAT(Time,'hh')+':'+FORMAT(Time,'mm')+':'+FORMAT(Time,'ss')) as Time FROM StudySessions ORDER BY Stack,Date;";
                List<StudyModel> sessions = connection.Query<StudyModel>(SqlCommandText).ToList();
                return sessions;
            }
        }

        internal List<StudyModel> GetOneStackStudySessions(string stack)
        {
            using (SqlConnection connection = new(ConnectionString))
            {
                SqlCommandText = @"SELECT Stack,QuestionMode,QuestionCount,Score,
                               (FORMAT(Time,'hh')+':'+FORMAT(Time,'mm')+':'+FORMAT(Time,'ss')) as Time,
                               (FORMAT(Date,'yyyy')+'-'+FORMAT(Date,'MM')+'-'+FORMAT(Date,'dd')) as Date FROM StudySessions ORDER BY Date;";
                List<StudyModel> sessions = connection.Query<StudyModel>(SqlCommandText).Where(x => x.Stack == stack).ToList();
                return sessions;
            }
        }

        internal List<StudyMonthly> GetMonthlyReports(string stack)
        {
            using (SqlConnection connection = new(ConnectionString))
            {
                SqlCommandText = $@"SELECT Format(Date,'yyyy-MM') as Month,
                                count(Format(Date,'yyyy-MM')) as QuestionCount ,
                                sum(Score) as TotalScore
                                FROM StudySessions WHERE Stack = @stack GROUP by Format(Date,'yyyy-MM') ORDER BY FORMAT(Date,'yyyy-MM');";
                List<StudyMonthly> monthly = new();
                monthly = connection.Query<StudyMonthly>(SqlCommandText, new { stack }).ToList();
                return monthly;
            }
        }
    }
}
