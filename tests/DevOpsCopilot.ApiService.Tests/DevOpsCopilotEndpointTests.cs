using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DevOpsCopilot.ApiService.Tests;

public class DevOpsCopilotEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
	private readonly HttpClient _client;

	public DevOpsCopilotEndpointTests(WebApplicationFactory<Program> factory)
	{
		_client = factory.CreateClient();
	}

	[Theory]
	[InlineData("", "member@example.com")]
	[InlineData("Ada", "")]
	[InlineData("   ", "   ")]
	public async Task Post_returns_400_when_adviser_name_or_member_email_is_blank(string adviserName, string memberEmail)
	{
		// Arrange
		var body = new
		{
			AdviserName = adviserName,
			MemeberEmail = memberEmail,
			SchemeName = "TestRun"
		};

		// Act
		var response = await _client.PostAsJsonAsync("/devops-copilot", body);

		// Assert
		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
	}
}
