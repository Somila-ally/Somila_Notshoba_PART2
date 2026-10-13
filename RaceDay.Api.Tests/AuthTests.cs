using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;
using RaceDay.Contracts;
using Xunit;

namespace RaceDay.Api.Tests
{
    public class AuthTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public AuthTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private static object RegisterPayload(string email, string role = "Participant", string password = "Password1!") => new
        {
            firstName = "Jane",
            lastName = "Tester",
            email,
            password,
            role
        };

        // ---------------------------------------------------------- registration

        [Theory]
        [InlineData("Participant")]
        [InlineData("Organiser")]
        public async Task Register_WithValidData_ReturnsCreated(string role)
        {
            var client = _factory.CreateSessionClient();

            var response = await client.PostAsJsonAsync("/api/auth/register",
                RegisterPayload(TestHelpers.UniqueEmail("valid"), role));

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
            Assert.Equal(role, body!.Role);
            Assert.Equal("Jane Tester", body.FullName);
        }

        [Fact]
        public async Task Register_WithInvalidRole_ReturnsBadRequest()
        {
            var client = _factory.CreateSessionClient();

            var response = await client.PostAsJsonAsync("/api/auth/register",
                RegisterPayload(TestHelpers.UniqueEmail("badrole"), "Admin"));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Register_WithDuplicateEmail_ReturnsBadRequest()
        {
            var client = _factory.CreateSessionClient();
            var payload = RegisterPayload(TestHelpers.UniqueEmail("dup"));

            await client.PostAsJsonAsync("/api/auth/register", payload);
            var second = await client.PostAsJsonAsync("/api/auth/register", payload);

            Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        }

        [Fact]
        public async Task Register_WithShortPassword_ReturnsBadRequest()
        {
            var client = _factory.CreateSessionClient();

            var response = await client.PostAsJsonAsync("/api/auth/register",
                RegisterPayload(TestHelpers.UniqueEmail("shortpw"), "Participant", "abc"));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Register_WithInvalidEmail_ReturnsBadRequest()
        {
            var client = _factory.CreateSessionClient();

            var response = await client.PostAsJsonAsync("/api/auth/register", RegisterPayload("not-an-email"));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Register_StoresHashedPassword_NotPlainText()
        {
            var client = _factory.CreateSessionClient();
            var email = TestHelpers.UniqueEmail("hash");

            await client.PostAsJsonAsync("/api/auth/register", RegisterPayload(email));

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == email);

            Assert.NotEqual("Password1!", user.PasswordHash);
            Assert.DoesNotContain("Password1!", user.PasswordHash);
            Assert.True(user.PasswordHash.Length > 20);
        }

        // ------------------------------------------------------------------ login

        [Fact]
        public async Task Login_WithCorrectCredentials_ReturnsOkWithRole()
        {
            var client = _factory.CreateSessionClient();
            var email = TestHelpers.UniqueEmail("login");
            await client.PostAsJsonAsync("/api/auth/register", RegisterPayload(email, "Organiser"));

            var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Password1!" });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
            Assert.Equal("Organiser", body!.Role);
        }

        [Fact]
        public async Task Login_WithWrongPassword_ReturnsUnauthorized()
        {
            var client = _factory.CreateSessionClient();
            var email = TestHelpers.UniqueEmail("wrongpw");
            await client.PostAsJsonAsync("/api/auth/register", RegisterPayload(email));

            var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "IncorrectPassword" });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Login_WithUnknownEmail_ReturnsUnauthorized()
        {
            var client = _factory.CreateSessionClient();

            var response = await client.PostAsJsonAsync("/api/auth/login",
                new { email = TestHelpers.UniqueEmail("ghost"), password = "Password1!" });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        // ------------------------------------------------------- session / logout

        [Fact]
        public async Task ProtectedEndpoint_WithoutLogin_ReturnsUnauthorized()
        {
            var client = _factory.CreateSessionClient();

            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/profile/me")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/events")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        }

        [Fact]
        public async Task Logout_ClearsSession_SoProfileBecomesUnauthorized()
        {
            var client = await TestHelpers.RegisterAndLoginAsync(_factory, "Participant");

            var logout = await client.PostAsync("/api/auth/logout", null);
            Assert.Equal(HttpStatusCode.OK, logout.StatusCode);

            var profile = await client.GetAsync("/api/profile/me");
            Assert.Equal(HttpStatusCode.Unauthorized, profile.StatusCode);
        }

        // ---------------------------------------------------------------- profile

        [Fact]
        public async Task GetProfile_AfterLogin_ReturnsOwnDetailsAndRole()
        {
            var email = TestHelpers.UniqueEmail("profile");
            var client = await TestHelpers.RegisterAndLoginAsync(_factory, "Organiser", email, "Amy", "Profile");

            var profile = await TestHelpers.GetProfileAsync(client);

            Assert.Equal(email, profile.Email);
            Assert.Equal("Organiser", profile.Role);
            Assert.Equal("Amy", profile.FirstName);
            Assert.Equal("Profile", profile.LastName);
        }

        [Fact]
        public async Task UpdateProfile_ChangesOnlyTheLoggedInUsersName()
        {
            var client = await TestHelpers.RegisterAndLoginAsync(_factory, "Participant", firstName: "Old", lastName: "Name");
            var bystander = await TestHelpers.RegisterAndLoginAsync(_factory, "Participant", firstName: "Bystander", lastName: "Untouched");

            var update = await client.PutAsJsonAsync("/api/profile/me", new { firstName = "New", lastName = "Surname" });
            Assert.Equal(HttpStatusCode.OK, update.StatusCode);

            var updated = await TestHelpers.GetProfileAsync(client);
            Assert.Equal("New", updated.FirstName);
            Assert.Equal("Surname", updated.LastName);

            var other = await TestHelpers.GetProfileAsync(bystander);
            Assert.Equal("Bystander", other.FirstName);
        }

        [Fact]
        public async Task UpdateProfile_WithoutLogin_ReturnsUnauthorized()
        {
            var client = _factory.CreateSessionClient();

            var response = await client.PutAsJsonAsync("/api/profile/me", new { firstName = "X", lastName = "Y" });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task UpdateProfile_WithMissingName_ReturnsBadRequest()
        {
            var client = await TestHelpers.RegisterAndLoginAsync(_factory, "Participant");

            var response = await client.PutAsJsonAsync("/api/profile/me", new { firstName = "", lastName = "" });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
