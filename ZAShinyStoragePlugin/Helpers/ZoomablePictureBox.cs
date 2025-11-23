namespace ZAShinyStoragePlugin
{
    public class ZoomablePictureBox : PictureBox
    {
        private float zoomFactor = 1.0f;
        private Point imageLocation = Point.Empty;
        private Point mouseDownLocation = Point.Empty;
        private bool isPanning = false;

        private const float MIN_ZOOM = 0.5f;
        private const float MAX_ZOOM = 10.0f;
        private const float ZOOM_STEP = 0.1f;

        public ZoomablePictureBox()
        {
            SizeMode = PictureBoxSizeMode.Normal;
            MouseWheel += OnMouseWheel;
            MouseDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseUp += OnMouseUp;
            Paint += OnPaint;
        }

        private void OnMouseWheel(object? sender, MouseEventArgs e)
        {
            if (Image == null) return;

            float oldZoom = zoomFactor;

            // Zoom in or out
            if (e.Delta > 0)
                zoomFactor += ZOOM_STEP;
            else
                zoomFactor -= ZOOM_STEP;

            // Clamp zoom
            zoomFactor = Math.Max(MIN_ZOOM, Math.Min(MAX_ZOOM, zoomFactor));

            // Adjust image location to zoom toward mouse cursor
            if (oldZoom != zoomFactor)
            {
                float zoomRatio = zoomFactor / oldZoom;
                imageLocation.X = (int)(e.X - (e.X - imageLocation.X) * zoomRatio);
                imageLocation.Y = (int)(e.Y - (e.Y - imageLocation.Y) * zoomRatio);
            }

            Invalidate();
        }

        private void OnMouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isPanning = true;
                mouseDownLocation = e.Location;
                Cursor = Cursors.SizeAll;
            }
        }

        private void OnMouseMove(object? sender, MouseEventArgs e)
        {
            if (isPanning)
            {
                int deltaX = e.X - mouseDownLocation.X;
                int deltaY = e.Y - mouseDownLocation.Y;

                imageLocation.X += deltaX;
                imageLocation.Y += deltaY;

                mouseDownLocation = e.Location;
                Invalidate();
            }
        }

        private void OnMouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isPanning = false;
                Cursor = Cursors.Default;
            }
        }

        private void OnPaint(object? sender, PaintEventArgs e)
        {
            if (Image == null) return;

            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

            int width = (int)(Image.Width * zoomFactor);
            int height = (int)(Image.Height * zoomFactor);

            Rectangle destRect = new(imageLocation.X, imageLocation.Y, width, height);
            e.Graphics.DrawImage(Image, destRect);
        }

        public void ResetZoom()
        {
            zoomFactor = 1.0f;
            imageLocation = Point.Empty;
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Invalidate();
        }
    }
}