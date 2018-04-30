#pragma once
#include "stdafx.h"
#pragma warning(push)
#pragma warning(disable : 4793)
#include <opencv2/imgproc/imgproc.hpp>
#include <opencv2/highgui/highgui.hpp>
#include <opencv2/objdetect/objdetect.hpp>
#include <opencv2/core/cvdef.h>
#include <opencv2/face.hpp>
#include "FaceLandmarkUnm.h"
#pragma warning(pop)


namespace Microsoft
{
	namespace Psi
	{
		namespace VideoAndAudio
		{
				void FaceLandmarkUnm::setFaceLandmark()
				{
					cv::face::FacemarkKazemi::Params params;
					// this->face_landmark = cv::face::FacemarkKazemi::create();
					// this->face_landmark->loadModel("C:/cv341/data/face-alignment/model/face_landmark_model.dat");
					this->face_landmark = cv::face::FacemarkLBF::create();
					this->face_landmark->loadModel("C:/cv341/data/face-alignment/model/lbfmodel.yaml");
				}
			
		}
	}
}