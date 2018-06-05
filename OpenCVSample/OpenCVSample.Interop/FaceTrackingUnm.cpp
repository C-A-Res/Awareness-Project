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
		void FaceTrackingUnm::setFaceTracker(int selection)
		{
			this->flag = 0;
			if (selection == 0)
			{
				this->tracker = cv::TrackerBoosting::create();
			}
			else if (selection == 1)
			{
				this->tracker = cv::TrackerMIL::create();
			}
			else if (selection == 2)
			{
				this->tracker = cv::TrackerKCF::create();
			}
			else if (selection == 3)
			{
				this->tracker = cv::TrackerTLD::create();
			}
			else if (selection == 4)
			{
				this->tracker = cv::TrackerMedianFlow::create();
			}
			else if (selection == 5)
			{
				this->tracker = cv::TrackerGOTURN::create();
			}
		}
	}
}