using Azure.Core;
using Azure.Data.Tables;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SendGrid;
using SendGrid.Helpers.Mail;
using System;
using System.Text.Json;

namespace DevOpsCopilot.Functions;

public class ProcessDevOpsCopilotRequest
{
	private readonly ILogger<ProcessDevOpsCopilotRequest> _logger;
	private readonly ISendGridClient _sendGrid;

	public ProcessDevOpsCopilotRequest(ILogger<ProcessDevOpsCopilotRequest> logger, ISendGridClient sendGrid)
	{
		_logger = logger;
		_sendGrid = sendGrid;
	}


	[Function(nameof(ProcessDevOpsCopilotRequest))]
	public async Task Run(
		[QueueTrigger("devops-copilot-request", Connection = "queues")] QueueMessage message,
		[TableInput("DevOpsCopilotAudit", Connection = "tables")] TableClient table,
		CancellationToken ct)
	{
		var payload = JsonSerializer.Deserialize<DevOpsCopilotPayload>(message.MessageText)
			?? throw new InvalidOperationException("Invalid payload");

		_logger.LogInformation("Processing DevOpsCopilot {TrackingId} for: {messageText}", payload.TrackingId, message.MessageText);

		// Send the confirmation email
		//var msg = new SendGridMessage
		//{
		//	From = new EmailAddress("devopscopilot@gmail.com", "DevOpsCopilot"),
		//	Subject = $"DevOpsCopilot request received - {payload.TrackingId}"
		//};
		var htmlContent = "<strong>and easy to do anywhere with C#.</strong>";
		var subject = $"DevOpsCopilot request received - {payload.TrackingId}";
		//msg.AddTo(payload.MemberEmail);
		var plainTextContent =
			$"Dear member,\n\n" +
			$"We've received a DevOpsCopilot request from adviser {payload.AdviserName}. " +
			$"Tracking ID: {payload.TrackingId}\n\n" +
			$"DevOpsCopilot (demo)";

		var mssg = MailHelper.CreateSingleEmail(new EmailAddress("kumarasiri@gmail.com", "DevOpsCopilot"), new EmailAddress(payload.MemberEmail), subject, plainTextContent, htmlContent);

		var response = await _sendGrid.SendEmailAsync(mssg, ct);

		if (!response.IsSuccessStatusCode)
		{
			_logger.LogWarning("SendGrid responsed {Status}", response.StatusCode);
		}

		// Update the audit row 
		await table.AddEntityAsync(new TableEntity(
			partitionKey: DateTime.UtcNow.ToString("yyyy-MM-dd"),
			rowKey: $"{payload.TrackingId}-processed")
		{
			["Status"] = "Processed",
			["TrackingId"] = payload.TrackingId,
			["EmailSentTo"] = payload.MemberEmail,
			["ProcessedAt"] = DateTime.UtcNow
		}, ct);


		_logger.LogInformation("DevOpsCopilot {TrackingId} processed", payload.TrackingId);

	}

	private record DevOpsCopilotPayload(string TrackingId, string AdviserName, string MemberEmail, string? SchemeName);
}