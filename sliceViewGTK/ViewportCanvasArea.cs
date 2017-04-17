﻿using System;
using System.Collections.Generic;
using Gtk;
using GLib;
using SkiaSharp;
using g3;
using gs;

namespace SliceViewer 
{
	class SliceViewCanvas : DrawingArea
	{
		public PathSet Paths = new PathSet();


		public bool ShowOpenEndpoints = true;

		public float Zoom = 0.95f;

		// this is a pixel-space translate
		public Vector2f Translate = Vector2f.Zero;


		public SliceViewCanvas() 
		{
			ExposeEvent += OnExpose;
		}


        SKPath MakePath(PolyLine2d polyLine, Func<Vector2d, SKPoint> mapF)
        {
			SKPath p = new SKPath();
            p.MoveTo(mapF(polyLine[0]));
			for ( int i = 1; i < polyLine.VertexCount; i++ )
				p.LineTo( mapF(polyLine[i]) );
            return p;
        }
		SKPath MakePath(PolyLine3d polyLine, Func<Vector2d, SKPoint> mapF)
		{
			SKPath p = new SKPath();
			p.MoveTo(mapF(polyLine[0].xy));
			for ( int i = 1; i < polyLine.VertexCount; i++ )
				p.LineTo( mapF(polyLine[i].xy) );
			return p;
		}        

		void OnExpose(object sender, ExposeEventArgs args)
		{
			DrawingArea area = (DrawingArea) sender;
			Cairo.Context cr =  Gdk.CairoHelper.Create(area.GdkWindow);

			int width = area.Allocation.Width;
			int height = area.Allocation.Height;

			//AxisAlignedBox3d bounds3 = Paths.Bounds;
			AxisAlignedBox3d bounds3 = Paths.ExtrudeBounds;
			AxisAlignedBox2d bounds = (bounds3 == AxisAlignedBox3d.Empty) ?
				new AxisAlignedBox2d(0, 0, 500, 500) : 
				new AxisAlignedBox2d(bounds3.Min.x, bounds3.Min.y, bounds3.Max.x, bounds3.Max.y );

			double sx = (double)width / bounds.Width;
			double sy = (double)height / bounds.Height;

			float scale = (float)Math.Min(sx, sy);

			// we apply this translate after scaling to pixel coords
			Vector2f pixC = Zoom * scale * (Vector2f)bounds.Center;
			Vector2f translate = new Vector2f(width/2, height/2) - pixC;


			//Zoom = 0.95f;
			//Zoom = 1.5f;
			//Translate = new Vector2f(0, 0);

            SKColorType useColorType = Util.IsRunningOnMono() ? SKColorType.Rgba8888 : SKColorType.Bgra8888;

			using (var bitmap = new SKBitmap(width, height, useColorType, SKAlphaType.Premul))
			{
				IntPtr len;
				using (var skSurface = SKSurface.Create(bitmap.Info.Width, bitmap.Info.Height, useColorType, SKAlphaType.Premul, bitmap.GetPixels(out len), bitmap.Info.RowBytes))
				{
					var canvas = skSurface.Canvas;
					canvas.Clear(new SKColor(240, 240, 240, 255));

					Func<Vector2d, Vector2f> xformF = (pOrig) => {
						Vector2f pNew = (Vector2f)pOrig;
						pNew -= (Vector2f)bounds.Center;
						pNew = Zoom * scale * pNew;
						pNew += (Vector2f)pixC;
						pNew += translate + Translate;
						pNew.y = canvas.ClipBounds.Height - pNew.y;
						return pNew;
					};
					Func<Vector2d, SKPoint> mapToSkiaF = (pOrig) => {
						Vector2f p = xformF(pOrig);
						return new SKPoint(p.x, p.y);
					};

					using (var paint = new SKPaint())
					{
						paint.StrokeWidth = 1;
						SKColor extrudeColor = new SKColor(0, 0, 0, 255);
						SKColor travelColor = new SKColor(0,255,0,128);
                        SKColor startColor = new SKColor(255, 0, 0, 128);
						SKColor planeColor = new SKColor(0,0,255, 128);
						float pointR = 3f;
						paint.IsAntialias = true;

						//paint.Style = SKPaintStyle.Fill;
                        paint.Style = SKPaintStyle.Stroke;

						Action<LinearPath2> drawPath2F = (polyPath) => {
							PolyLine2d poly = polyPath.Path;
							SKPath path = MakePath(poly, mapToSkiaF);
							paint.Color = (polyPath.Type == PathTypes.Deposition ) ? extrudeColor : travelColor;
							paint.StrokeWidth = (polyPath.Type == PathTypes.Deposition ) ? 1 : 3;
							canvas.DrawPath(path, paint);
							paint.Color = startColor;
							paint.StrokeWidth = 1;
							Vector2f pt = xformF(poly.Start);
							canvas.DrawCircle(pt.x, pt.y, pointR, paint);						
						};
						Action<LinearPath3> drawPath3F = (polyPath) => {
							PolyLine3d poly = polyPath.Path;
							SKPath path = MakePath(poly, mapToSkiaF);
							paint.Color = planeColor;
							paint.StrokeWidth = 1;
							canvas.DrawPath(path, paint);
							paint.StrokeWidth = 1;
							Vector2f pt = xformF(poly.Start.xy);
							canvas.DrawCircle(pt.x, pt.y, 5f, paint);						
							paint.Color = startColor;
						};
						Action<IPath> drawPath = (path) => {
							if ( path is LinearPath3 )
								drawPath3F(path as LinearPath3);
							else if (path is LinearPath2)
								drawPath2F(path as LinearPath2);
							else
								throw new NotImplementedException();
						};
						Action<IPathSet> drawPaths = null;
						drawPaths = (paths) => {
							foreach ( IPath path in paths ) {
								if ( path is IPathSet )
									drawPaths(path as IPathSet);
								else
									drawPath(path);
							}
						};

						drawPaths(Paths);

					}

					Cairo.Surface surface = new Cairo.ImageSurface(
						bitmap.GetPixels(out len),
						Cairo.Format.Argb32,
						bitmap.Width, bitmap.Height,
						bitmap.Width * 4);

					surface.MarkDirty();
					cr.SetSourceSurface(surface, 0, 0);
					cr.Paint();
				}
			}

			//return true;
		}
	}
}
