#pragma once
#include "stdafx.h"
#pragma warning(push)
#pragma warning(disable : 4793)
#include <opencv2/imgproc/imgproc.hpp>
#include <opencv2/highgui/highgui.hpp>
#include <opencv2/objdetect/objdetect.hpp>
#include <opencv2/core/cvdef.h>
#include <opencv2/tracking/tracking.hpp>
#include "FaceTrackingUnm.h"
#pragma warning(pop)


namespace NU
{
	namespace Kiosk
	{
		void FaceTrackingUnm::setFaceTracker()
		{
			this->flag = 0;
			this->tracker = cv::TrackerMIL::create();
		}
	}
}