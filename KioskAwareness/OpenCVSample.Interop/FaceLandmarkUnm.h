#pragma once
#include "stdafx.h"
#pragma warning(push)
#pragma warning(disable : 4793)
#include <opencv2/imgproc/imgproc.hpp>
#include <opencv2/highgui/highgui.hpp>
#include <opencv2/objdetect/objdetect.hpp>
#include <opencv2/core/cvdef.h>
#include <opencv2/face.hpp>
#pragma warning(pop)


namespace Microsoft
{
	namespace Psi
	{
		namespace VideoAndAudio
		{
				public class FaceLandmarkUnm
				{
				public:
					// cv::Ptr<cv::face::FacemarkKazemi> face_landmark;
					cv::Ptr<cv::face::Facemark> face_landmark;
					void setFaceLandmark();
				};
			
		}
	}
}