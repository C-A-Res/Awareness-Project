#include "stdafx.h"
#pragma warning(push)
#pragma warning(disable : 4793)
#include <opencv2/imgproc/imgproc.hpp>
#include <opencv2/highgui/highgui.hpp>
#include <opencv2/objdetect/objdetect.hpp>
#include <opencv2/core/cvdef.h>
#include <opencv2/face.hpp>
#include "CascadeClassifierUnm.h"
#include "FaceLandmarkUnm.h"
#pragma warning(pop)


namespace Microsoft
{
	namespace Psi
	{
		namespace VideoAndAudio
		{
				public ref class FaceCasClassifier
				{
				public:
					CascadeClassifierUnm * face_cascade;
					FaceLandmarkUnm * facemark;
					// cv::CascadeClassifier* classifier;
					// cv::face::FacemarkKazemi * facemark;
					FaceCasClassifier();

				};
		}
	}
}
