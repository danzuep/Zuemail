﻿using CommunityToolkit.Common;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace Zuemail.Models;

public class Email
{
    [Key]
    [NotNull]
    public Guid Uid { get; set; } = Guid.NewGuid();

    public string MailboxFolder { get; set; } = string.Empty;

    public int MailboxIndex { get; set; }

    public string MessageId { get; set; } = string.Empty;

    public string Date { get; set; } = string.Empty;

    public string From { get; set; } = string.Empty;

    public string ReplyTo { get; set; } = string.Empty;

    public string To { get; set; } = string.Empty;

    public string Cc { get; set; } = string.Empty;

    public string Bcc { get; set; } = string.Empty;

    public string Headers { get; set; } = string.Empty;

    public string Attachments { get; set; } = string.Empty;

    public List<EmailAttachment> EmailAttachments { get; set; } = new();

    public string Subject { get; set; } = string.Empty;

    public int PreviewLength { get; set; } = 50;

    private string _bodyPreview = string.Empty;
    public string BodyPreview
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_bodyPreview) &&
                !string.IsNullOrWhiteSpace(BodyHtml))
            {
                _bodyPreview = BodyHtml.DecodeHtml();
                if (string.IsNullOrWhiteSpace(BodyText))
                    BodyText = _bodyPreview;
                if (_bodyPreview.Length > PreviewLength)
                    _bodyPreview = $"{_bodyPreview.Substring(0, PreviewLength)} ...";
            }
            return _bodyPreview;
        }
        set => _bodyPreview = value;
    }

    public string BodyText { get; set; } = string.Empty;

    public string BodyHtml { get; set; } = string.Empty;

    public Email Copy()
    {
        var email = MemberwiseClone() as Email ?? new Email();
        email.Uid = Guid.NewGuid();
        return email;
    }

    public override string ToString()
    {
        string envelope = string.Empty;
        using (var text = new StringWriter())
        {
            text.WriteLine("SentDate: {0}", Date);
            text.WriteLine("Received: {0}", DateTimeOffset.Now);
            if (!string.IsNullOrEmpty(From))
                text.WriteLine("From: {0}", From);
            if (!string.IsNullOrEmpty(To))
                text.WriteLine("To: {0}", To);
            if (!string.IsNullOrEmpty(Cc))
                text.WriteLine("Cc: {0}", Cc);
            if (!string.IsNullOrEmpty(Bcc))
                text.WriteLine("Bcc: {0}", Bcc);
            text.WriteLine("Subject: {0}", Subject);
            if (!string.IsNullOrEmpty(Attachments))
                text.WriteLine("Attachments: {0}", Attachments);
            envelope = text.ToString();
        }
        return envelope;
    }
}