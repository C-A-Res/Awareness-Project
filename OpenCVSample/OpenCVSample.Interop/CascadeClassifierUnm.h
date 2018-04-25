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
		namespace Samples
		{
			namespace OpenCV
			{
				public class CascadeClassifierUnm
				{
				public:
					cv::CascadeClassifier face_cas;
					void setFaceDetector();
				};
			}
		}
	}
}