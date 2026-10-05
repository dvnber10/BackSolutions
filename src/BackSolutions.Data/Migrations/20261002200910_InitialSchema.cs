using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BackSolutions.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DescriptionHtml = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CoverImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CoverImageAlt = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ClientName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TechStackJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    DeliveredOn = table.Column<DateOnly>(type: "date", nullable: true),
                    LiveUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RepositoryUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsFeatured = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Services",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    ShortDescription = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    FullDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Icon = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TechnologiesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BasePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PriceNote = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Services", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SiteSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    LegalName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TaxId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LogoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FaviconUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ContactEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    WhatsApp = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    FacebookUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    InstagramUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LinkedInUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    GithubUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    XUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HeroTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    HeroSubtitle = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    HeroImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HeroPrimaryCtaText = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    HeroPrimaryCtaUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    HeroSecondaryCtaText = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    HeroSecondaryCtaUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    FooterText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FooterLegalText = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DefaultSeoTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DefaultSeoDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DefaultOgImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DefaultCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ProposalValidityDays = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    AvatarUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    MustChangePassword = table.Column<bool>(type: "bit", nullable: false),
                    FailedLoginAttempts = table.Column<int>(type: "int", nullable: false),
                    LockoutEndsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastLoginAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProjectImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AltText = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectImages_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BlogPosts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Excerpt = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ContentHtml = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CoverImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CoverImageAlt = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SeoTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SeoDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReadingMinutes = table.Column<int>(type: "int", nullable: false),
                    ViewCount = table.Column<long>(type: "bigint", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlogPosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BlogPosts_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Leads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PublicToken = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    ServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    BudgetMin = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    BudgetMax = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    TargetStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    InternalNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HandledByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Leads_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Leads_Users_HandledByUserId",
                        column: x => x.HandledByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Pages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Eyebrow = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Intro = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SeoTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SeoDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OgImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ShowInNavigation = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pages_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReplacedByTokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DeviceName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GrantedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BlogPostTags",
                columns: table => new
                {
                    PostId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TagId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlogPostTags", x => new { x.PostId, x.TagId });
                    table.ForeignKey(
                        name: "FK_BlogPostTags_BlogPosts_PostId",
                        column: x => x.PostId,
                        principalTable: "BlogPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BlogPostTags_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Conversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PublicToken = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LeadId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClientName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    ClientEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    ClientPhone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    HighestSeverity = table.Column<int>(type: "int", nullable: false),
                    AssignedToUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FirstResponseAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastMessageAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CloseReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Conversations_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Conversations_Users_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Proposals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    LeadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    SummaryHtml = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NotesHtml = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ViewedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ValidUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Proposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Proposals_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Proposals_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PageSections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Subtitle = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ContentJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsVisible = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PageSections_Pages_PageId",
                        column: x => x.PageId,
                        principalTable: "Pages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConversationParticipants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JoinedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LeftAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastReadAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationParticipants_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConversationParticipants_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Escalations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Severity = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RaisedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedToUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResolutionNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ResolvedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Escalations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Escalations_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Escalations_Users_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Escalations_Users_RaisedByUserId",
                        column: x => x.RaisedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SenderType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SenderUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsInternal = table.Column<bool>(type: "bit", nullable: false),
                    AttachmentName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    AttachmentContentType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    AttachmentContent = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReadAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Messages_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Messages_Users_SenderUserId",
                        column: x => x.SenderUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProposalItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProposalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProposalItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProposalItems_Proposals_ProposalId",
                        column: x => x.ProposalId,
                        principalTable: "Proposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "CreatedAtUtc", "Description", "IsSystem", "Name", "UpdatedAtUtc" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Dueño del sistema. Acceso total.", true, "Owner", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("22222222-2222-2222-2222-222222222222"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Administra contenido, leads, usuarios y conversaciones.", true, "Admin", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("33333333-3333-3333-3333-333333333333"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Trabaja leads y conversaciones asignadas.", true, "Provider", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("44444444-4444-4444-4444-444444444444"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Atiende conversaciones de chat.", true, "Support", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
                });

            migrationBuilder.InsertData(
                table: "SiteSettings",
                columns: new[] { "Id", "Address", "CompanyName", "ContactEmail", "CreatedAtUtc", "DefaultCurrency", "DefaultOgImageUrl", "DefaultSeoDescription", "DefaultSeoTitle", "FacebookUrl", "FaviconUrl", "FooterLegalText", "FooterText", "GithubUrl", "HeroImageUrl", "HeroPrimaryCtaText", "HeroPrimaryCtaUrl", "HeroSecondaryCtaText", "HeroSecondaryCtaUrl", "HeroSubtitle", "HeroTitle", "InstagramUrl", "LegalName", "LinkedInUrl", "LogoUrl", "Phone", "ProposalValidityDays", "TaxId", "UpdatedAtUtc", "WhatsApp", "XUrl" },
                values: new object[] { 1, null, "BackSolutions", "contacto@backsolutions.dev", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "USD", null, "Agencia de desarrollo de software: sitios web, aplicaciones, integraciones y automatización.", "BackSolutions | Soluciones digitales a medida", null, null, null, "© BackSolutions. Todos los derechos reservados.", null, null, "Solicitar cotización", "/contact", "Ver portafolio", "/portfolio", "Desarrollamos software que resuelve problemas reales de negocio.", "Soluciones digitales a medida", null, "BackSolutions", null, null, null, 30, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null });

            migrationBuilder.CreateIndex(
                name: "IX_BlogPosts_AuthorId",
                table: "BlogPosts",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_BlogPosts_Status_PublishedAtUtc",
                table: "BlogPosts",
                columns: new[] { "Status", "PublishedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_BlogPosts_Slug",
                table: "BlogPosts",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BlogPostTags_TagId",
                table: "BlogPostTags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationParticipants_UserId",
                table: "ConversationParticipants",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UX_ConversationParticipants_ConversationId_UserId",
                table: "ConversationParticipants",
                columns: new[] { "ConversationId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_AssignedToUserId",
                table: "Conversations",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_HighestSeverity",
                table: "Conversations",
                column: "HighestSeverity");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_LeadId",
                table: "Conversations",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_Status_LastMessageAtUtc",
                table: "Conversations",
                columns: new[] { "Status", "LastMessageAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_Conversations_PublicToken",
                table: "Conversations",
                column: "PublicToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Escalations_AssignedToUserId",
                table: "Escalations",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Escalations_ConversationId",
                table: "Escalations",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_Escalations_RaisedByUserId",
                table: "Escalations",
                column: "RaisedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Escalations_Status_Severity",
                table: "Escalations",
                columns: new[] { "Status", "Severity" });

            migrationBuilder.CreateIndex(
                name: "IX_Leads_Email",
                table: "Leads",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_HandledByUserId",
                table: "Leads",
                column: "HandledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_ServiceId",
                table: "Leads",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_Status_CreatedAtUtc",
                table: "Leads",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_Leads_PublicToken",
                table: "Leads",
                column: "PublicToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ConversationId_SentAtUtc",
                table: "Messages",
                columns: new[] { "ConversationId", "SentAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_SenderUserId",
                table: "Messages",
                column: "SenderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Pages_AuthorId",
                table: "Pages",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "UX_Pages_Slug",
                table: "Pages",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PageSections_PageId_SortOrder",
                table: "PageSections",
                columns: new[] { "PageId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectImages_ProjectId_SortOrder",
                table: "ProjectImages",
                columns: new[] { "ProjectId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Status_SortOrder",
                table: "Projects",
                columns: new[] { "Status", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "UX_Projects_Slug",
                table: "Projects",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProposalItems_ProposalId_SortOrder",
                table: "ProposalItems",
                columns: new[] { "ProposalId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Proposals_CreatedByUserId",
                table: "Proposals",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Proposals_LeadId",
                table: "Proposals",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "UX_Proposals_Number",
                table: "Proposals",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UX_RefreshTokens_TokenHash",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Services_Status_SortOrder",
                table: "Services",
                columns: new[] { "Status", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "UX_Services_Slug",
                table: "Services",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Tags_Name",
                table: "Tags",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Tags_Slug",
                table: "Tags",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "UX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BlogPostTags");

            migrationBuilder.DropTable(
                name: "ConversationParticipants");

            migrationBuilder.DropTable(
                name: "Escalations");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "PageSections");

            migrationBuilder.DropTable(
                name: "ProjectImages");

            migrationBuilder.DropTable(
                name: "ProposalItems");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "SiteSettings");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "BlogPosts");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropTable(
                name: "Conversations");

            migrationBuilder.DropTable(
                name: "Pages");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropTable(
                name: "Proposals");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Leads");

            migrationBuilder.DropTable(
                name: "Services");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
