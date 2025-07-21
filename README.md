# CVexplorer Backend

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE.txt)

## Description
The backend API for **CVexplorer**, a service which allows users to upload, manage and evaluate CVs using a dedicated LLM evaluation microservice. It provides secure OAuth authentication and exposes REST endpoints for all core operations.

## Integration
This backend service works together with:
- **Frontend UI**: https://github.com/username/proiect-frontend  
- **LLM Evaluation Service**: https://github.com/username/llm-evaluator
  
## Technologies Used
- **.NET 8** with C#  
- **Entity Framework Core** (SQL Server provider)  
- **ASP.NET Core Web API**  
- **OAuth 2.0 / OpenID Connect** with Google & Microsoft  
- **Swagger (Swashbuckle)** for interactive API docs  
- **JWT Authentication** using JSON Web Tokens for secure, stateless user sessions
- **ASP.NET Core Identity** for user and role management  


## Implemented Functionalities
- **Company Management**: create, read, update, and delete companies  
- **Global User Management**: manage all users across the platform (CRUD operations)  
- **Company-Specific User Administration**: administer users within a particular company   
- **Department & Position Management**: CRUD operations for departments and job positions to organize candidate submissions  
- **CV Upload**  
  - Manual single and bulk document upload  
  - Automated upload via Gmail and Outlook integration  
- **Evaluation**  
  - Automated evaluation using the dedicated LLM Evaluation Service  
  - Manual evaluation workflow for reviewer input  
- **Evaluation Round Management**: define and manage evaluation rounds for candidate classification per position  

## Configuration
1. Copy `appsettings.json` to `appsettings.Development.json` (if needed).  
2. Edit the connection strings in **appsettings.json**:
   ```jsonc
   {
     "ConnectionStrings": {
       "LocalConnection": "Server=localhost;Database=CVexplorerDb;Trusted_Connection=True;
     }
   }
3. Add `OAuth` credentials via User Secrets:
    ```jsonc
    dotnet user-secrets init
    dotnet user-secrets set "Google:ClientId"     "<YOUR_GOOGLE_CLIENT_ID>"
    dotnet user-secrets set "Google:ClientSecret" "<YOUR_GOOGLE_CLIENT_SECRET>"
    dotnet user-secrets set "Microsoft:AzureAd:ClientId"     "<YOUR_MS_CLIENT_ID>"
    dotnet user-secrets set "Microsoft:AzureAd:ClientSecret" "<YOUR_MS_CLIENT_SECRET>"

## Project Structure

- **/Controllers**: Contains controller classes that define and route API endpoints.  
- **/Data**: Houses the `DataContext` class and EF Core configuration for database interactions.  
- **/Enums**: Defines shared enumeration types used across the application.  
- **/Exceptions**: Implements custom exception classes and error‑handling middleware.  
- **/Extensions**: Includes extension methods to configure services:  
  - `ApplicationServiceExtensions.cs` (sets up controllers, DbContext, CORS, DI for repos/services, background tasks, HTTP clients, email integrations)  
  - `IdentityServiceExtensions.cs` (configures ASP.NET Core Identity, authentication schemes, JWT options, and role policies)  
- **/Helpers**: Provides utility classes such as AutoMapper profiles.  
- **/Migrations**: Stores EF Core migration files for evolving the database schema.  
- **/Models**: Defines domain entities, DTOs, and primitive value objects, organized into subfolders (`Domain`, `DTO`, `Primitives`).  
- **/Repositories**: Implements the Repository pattern with `Interface` (IRepository definitions) and `Implementation` (concrete EF Core logic).  
- **/Services**: Contains business logic and external integrations with `Interface` (IService contracts) and `Implementation` (LLM evaluation calls, email ingestion).  



