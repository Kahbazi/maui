﻿using CoreAnimation;
using CoreGraphics;
using System;
using UIKit;
using System.ComponentModel;

namespace Xamarin.Forms.Platform.iOS
{
	public class ShellTableViewController : UITableViewController
	{
		readonly IShellContext _context;
		readonly UIContainerView _headerView;
		readonly ShellTableViewSource _source;
		double _headerMin = 56;
		double _headerOffset = 0;
		double _headerSize;
		bool _isDisposed;

		public ShellTableViewController(IShellContext context, UIContainerView headerView, Action<Element> onElementSelected)
		{
			_context = context;
			_headerView = headerView;
			_source = new ShellTableViewSource(context, onElementSelected);
			_source.ScrolledEvent += OnScrolled;
			if (_headerView != null)
				_headerView.HeaderSizeChanged += OnHeaderSizeChanged;
			((IShellController)_context.Shell).StructureChanged += OnStructureChanged;

			_context.Shell.PropertyChanged += OnShellPropertyChanged;
		}

		void OnShellPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == Shell.FlyoutHeaderBehaviorProperty.PropertyName)
			{
				SetHeaderContentInset();
				LayoutParallax();
			}
			else if (e.Is(Shell.FlyoutVerticalScrollModeProperty))
				UpdateVerticalScrollMode();
		}

		void OnHeaderSizeChanged(object sender, EventArgs e)
		{
			_headerSize = HeaderMax;
			SetHeaderContentInset();
			LayoutParallax();
		}

		void OnStructureChanged(object sender, EventArgs e)
		{
			_source.ClearCache();
			TableView.ReloadData();
			UpdateVerticalScrollMode();
		}

		void UpdateVerticalScrollMode()
		{
			switch (_context.Shell.FlyoutVerticalScrollMode)
			{
				case ScrollMode.Auto:
					var pathToFirstRow = Foundation.NSIndexPath.FromRowSection(0, 0);
					var firstCellRect = TableView.RectForRowAtIndexPath(pathToFirstRow);
					var firstCellIsVisible = TableView.Bounds.Contains(firstCellRect);

					var lastRowIndex = NMath.Max(0, TableView.NumberOfRowsInSection(0) - 1);
					var pathToLastRow = Foundation.NSIndexPath.FromRowSection(lastRowIndex, 0);
					var cellRect = TableView.RectForRowAtIndexPath(pathToLastRow);
					var lastCellIsVisible = TableView.Bounds.Contains(cellRect);

					TableView.ScrollEnabled = !firstCellIsVisible || !lastCellIsVisible;
					break;
				case ScrollMode.Enabled:
					TableView.ScrollEnabled = true;
					break;
				case ScrollMode.Disabled:
					TableView.ScrollEnabled = false;
					break;
			}
		}

		public void LayoutParallax()
		{
			if (TableView?.Superview == null)
				return;

			var parent = TableView.Superview;

			if (_headerView != null)
				TableView.Frame =
					new CGRect(parent.Bounds.X, HeaderTopMargin, parent.Bounds.Width, parent.Bounds.Height - HeaderTopMargin);
			else
				TableView.Frame = parent.Bounds;

			if (_headerView != null)
			{
				var margin = _headerView.Margin;
				var leftMargin = margin.Left - margin.Right;

				_headerView.Frame = new CGRect(leftMargin, _headerOffset + HeaderTopMargin, parent.Frame.Width, _headerSize);

				if (_context.Shell.FlyoutHeaderBehavior == FlyoutHeaderBehavior.Scroll && HeaderTopMargin > 0 && _headerOffset < 0)
				{
					var headerHeight = Math.Max(_headerMin, _headerSize + _headerOffset);
					CAShapeLayer shapeLayer = new CAShapeLayer();
					CGRect rect = new CGRect(0, _headerOffset * -1, parent.Frame.Width, headerHeight);
					var path = CGPath.FromRect(rect);
					shapeLayer.Path = path;
					_headerView.Layer.Mask = shapeLayer;
				}
				else if (_headerView.Layer.Mask != null)
					_headerView.Layer.Mask = null;
			}
		}

		void SetHeaderContentInset()
		{
			if (_headerView != null)
				TableView.ContentInset = new UIEdgeInsets((nfloat)HeaderMax, 0, 0, 0);
			else
				TableView.ContentInset = new UIEdgeInsets(Platform.SafeAreaInsetsForWindow.Top, 0, 0, 0);
			UpdateVerticalScrollMode();
		}

		public override void ViewDidLoad()
		{
			base.ViewDidLoad();
			_headerView?.MeasureIfNeeded();

			TableView.SeparatorStyle = UITableViewCellSeparatorStyle.None;
			if (Forms.IsiOS11OrNewer)
				TableView.ContentInsetAdjustmentBehavior = UIScrollViewContentInsetAdjustmentBehavior.Never;

			SetHeaderContentInset();
			TableView.Source = _source;
		}

		protected override void Dispose(bool disposing)
		{
			if (_isDisposed)
				return;

			if (disposing)
			{
				if ((_context?.Shell as IShellController) != null)
					((IShellController)_context.Shell).StructureChanged -= OnStructureChanged;

				if (_source != null)
					_source.ScrolledEvent -= OnScrolled;

				if (_headerView != null)
					_headerView.HeaderSizeChanged -= OnHeaderSizeChanged;

				_context.Shell.PropertyChanged -= OnShellPropertyChanged;
			}

			_isDisposed = true;
			base.Dispose(disposing);
		}


		void OnScrolled(object sender, UIScrollView e)
		{
			if (_headerView == null)
				return;

			var headerBehavior = _context.Shell.FlyoutHeaderBehavior;

			switch (headerBehavior)
			{
				case FlyoutHeaderBehavior.Default:
				case FlyoutHeaderBehavior.Fixed:
					_headerSize = HeaderMax;
					_headerOffset = 0;
					break;

				case FlyoutHeaderBehavior.Scroll:
					_headerSize = HeaderMax;
					_headerOffset = Math.Min(0, -(HeaderMax + e.ContentOffset.Y));
					break;

				case FlyoutHeaderBehavior.CollapseOnScroll:
					_headerSize = Math.Max(_headerMin, -e.ContentOffset.Y);
					_headerOffset = 0;
					break;
			}

			LayoutParallax();
		}

		double HeaderMax => _headerView?.MeasuredHeight ?? 0;
		double HeaderTopMargin => (_headerView != null) ? _headerView.Margin.Top - _headerView.Margin.Bottom : 0;
	}
}