﻿namespace Zuemail.Controls;

public class Selected : IDrawable
{
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FontSize = 18;

        canvas.FontColor = Color.FromArgb("0078d4");

        canvas.DrawString("Inbox", 10, 0, 60, 25, HorizontalAlignment.Center, VerticalAlignment.Center);

        canvas.DrawString("Other", 75, 0, 70, 25, HorizontalAlignment.Center, VerticalAlignment.Center);
    }
}
