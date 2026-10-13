using System.Net;
using System.Net.Http.Json;
using RaceDay.Contracts;
using Xunit;

namespace RaceDay.Api.Tests
{
    public class CategoryTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public CategoryTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task CreateCategory_ByOwner_ReturnsCreated_AndDistanceDefaultsToEventDistance()
        {
            var owner = await TestHelpers.RegisterAndLoginAsync(_factory, "Organiser");
            var ev = await TestHelpers.CreateEventAsync(owner);

            var response = await owner.PostAsJsonAsync($"/api/events/{ev.EventId}/categories", new { name = "Under 20" });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var category = await response.Content.ReadFromJsonAsync<CategoryDto>();
            Assert.Equal("Under 20", category!.Name);
            Assert.Equal(ev.DistanceKm, category.DistanceKm);
        }

        [Fact]
        public async Task CreateCategory_WithOwnDistanceAndFee_StoresThem()
        {
            var owner = await TestHelpers.RegisterAndLoginAsync(_factory, "Organiser");
            var ev = await TestHelpers.CreateEventAsync(owner);

            var response = await owner.PostAsJsonAsync($"/api/events/{ev.EventId}/categories", new
            {
                name = "5km",
                distanceKm = 5m,
                maxParticipants = 50,
                entryFee = 75.5m
            });

            var category = await response.Content.ReadFromJsonAsync<CategoryDto>();
            Assert.Equal(5m, category!.DistanceKm);
            Assert.Equal(50, category.MaxParticipants);
            Assert.Equal(75.5m, category.EntryFee);
        }

        [Fact]
        public async Task CreateCategory_ByNonOwningOrganiser_ReturnsForbidden()
        {
            var owner = await TestHelpers.RegisterAndLoginAsync(_factory, "Organiser");
            var other = await TestHelpers.RegisterAndLoginAsync(_factory, "Organiser");
            var ev = await TestHelpers.CreateEventAsync(owner);

            var response = await other.PostAsJsonAsync($"/api/events/{ev.EventId}/categories", new { name = "Sneaky" });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task CreateCategory_AsParticipant_ReturnsForbidden()
        {
            var owner = await TestHelpers.RegisterAndLoginAsync(_factory, "Organiser");
            var participant = await TestHelpers.RegisterAndLoginAsync(_factory, "Participant");
            var ev = await TestHelpers.CreateEventAsync(owner);

            var response = await participant.PostAsJsonAsync($"/api/events/{ev.EventId}/categories", new { name = "Nope" });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task CreateCategory_ForMissingEvent_ReturnsNotFound()
        {
            var organiser = await TestHelpers.RegisterAndLoginAsync(_factory, "Organiser");
            var response = await organiser.PostAsJsonAsync("/api/events/999999/categories", new { name = "Ghost" });
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task GetCategories_AsParticipant_ReturnsTheEventsCategories()
        {
            var owner = await TestHelpers.RegisterAndLoginAsync(_factory, "Organiser");
            var participant = await TestHelpers.RegisterAndLoginAsync(_factory, "Participant");
            var ev = await TestHelpers.CreateEventAsync(owner);
            await TestHelpers.CreateCategoryAsync(owner, ev.EventId, "Junior");
            await TestHelpers.CreateCategoryAsync(owner, ev.EventId, "Senior");

            var response = await participant.GetAsync($"/api/events/{ev.EventId}/categories");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var categories = await response.Content.ReadFromJsonAsync<List<CategoryDto>>();
            Assert.Equal(2, categories!.Count);
        }

        [Fact]
        public async Task UpdateCategory_ByOwner_ChangesIt_ButNotByOtherOrganiser()
        {
            var owner = await TestHelpers.RegisterAndLoginAsync(_factory, "Organiser");
            var other = await TestHelpers.RegisterAndLoginAsync(_factory, "Organiser");
            var ev = await TestHelpers.CreateEventAsync(owner);
            var category = await TestHelpers.CreateCategoryAsync(owner, ev.EventId, "Before");

            var forbidden = await other.PutAsJsonAsync($"/api/categories/{category.CategoryId}", new { name = "Hijacked" });
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

            var ok = await owner.PutAsJsonAsync($"/api/categories/{category.CategoryId}", new { name = "After", maxParticipants = 20 });
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
            var updated = await ok.Content.ReadFromJsonAsync<CategoryDto>();
            Assert.Equal("After", updated!.Name);
            Assert.Equal(20, updated.MaxParticipants);
            Assert.Equal(category.DistanceKm, updated.DistanceKm);
        }

        [Fact]
        public async Task UpdateCategory_ThatDoesNotExist_ReturnsNotFound()
        {
            var organiser = await TestHelpers.RegisterAndLoginAsync(_factory, "Organiser");
            var response = await organiser.PutAsJsonAsync("/api/categories/999999", new { name = "Ghost" });
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task DeleteCategory_ByOwner_ReturnsOk_ButNotByOtherOrganiser()
        {
            var owner = await TestHelpers.RegisterAndLoginAsync(_factory, "Organiser");
            var other = await TestHelpers.RegisterAndLoginAsync(_factory, "Organiser");
            var ev = await TestHelpers.CreateEventAsync(owner);
            var category = await TestHelpers.CreateCategoryAsync(owner, ev.EventId);

            var forbidden = await other.DeleteAsync($"/api/categories/{category.CategoryId}");
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

            var deleted = await owner.DeleteAsync($"/api/categories/{category.CategoryId}");
            Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        }

        [Fact]
        public async Task DeleteCategory_WithEnrolments_ReturnsBadRequest()
        {
            var owner = await TestHelpers.RegisterAndLoginAsync(_factory, "Organiser");
            var ev = await TestHelpers.CreateEventAsync(owner);
            var category = await TestHelpers.CreateCategoryAsync(owner, ev.EventId);
            var participant = await TestHelpers.RegisterAndLoginAsync(_factory, "Participant");
            await TestHelpers.EnrolAsync(participant, ev.EventId, category.CategoryId);

            var response = await owner.DeleteAsync($"/api/categories/{category.CategoryId}");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
