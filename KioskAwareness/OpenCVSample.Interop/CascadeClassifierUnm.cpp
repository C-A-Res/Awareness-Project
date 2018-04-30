#pragma once
#include "stdafx.h"
#pragma warning(push)
#pragma warning(disable : 4793)
#include <opencv2/imgproc/imgproc.hpp>
#include <opencv2/highgui/highgui.hpp>
#include <opencv2/objdetect/objdetect.hpp>
#include <opencv2/core/cvdef.h>
#include <opencv2/face.hpp>
#include "CascadeClassifierUnm.h"
#pragma warning(pop)


namespace Microsoft
{
	namespace Psi
	{
		namespace VideoAndAudio
		{
				void CascadeClassifierUnm::setFaceDetector()
				{
					this->face_cas.load("C:/cv341/opencv/sources/data/haarcascades/haarcascade_frontalface_alt.xml");
				}
			
		}
	}
}