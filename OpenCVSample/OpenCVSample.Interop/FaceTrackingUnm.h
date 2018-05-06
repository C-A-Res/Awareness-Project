#pragma once
#pragma once
#include "stdafx.h"
#pragma warning(push)
#pragma warning(disable : 4793)
#include <opencv2/imgproc/imgproc.hpp>
#include <opencv2/highgui/highgui.hpp>
#include <opencv2/objdetect/objdetect.hpp>
#include <opencv2/core/cvdef.h>
#include <opencv2/tracking/tracking.hpp>
#pragma warning(pop)


namespace NU
{
	namespace Kiosk
	{
		public class FaceTrackingUnm
		{
		public:
			int flag;
			cv::Rect2d previous_bbox;
			cv::Ptr<cv::Tracker> tracker;
			void setFaceTracker();
		};
	}
}