#include "stdafx.h"
#include "FaceCasClassifier.h"
#pragma warning(push)
#pragma warning(disable : 4793)
#include <opencv2/imgproc/imgproc.hpp>
#include <opencv2/highgui/highgui.hpp>
#include <opencv2/objdetect/objdetect.hpp>
#include <opencv2/core/cvdef.h>
#include <opencv2/face.hpp>
#include <opencv2/face/face_alignment.hpp>
#pragma warning(pop)

namespace NU
{
	namespace Kiosk
	{
				
				FaceCasClassifier::FaceCasClassifier()
				{
					/*
					this->classifier = new cv::CascadeClassifier;
					this->classifier->load("C:/cv341/opencv/sources/data/haarcascades/haarcascade_frontalface_alt.xml");
					cv::face::FacemarkKazemi::Params params;
					this->facemark = cv::face::FacemarkKazemi::create(params);
					this->facemark->loadModel("C:/cv341/data/face-alignment/model/face_landmark_model.dat");
					*/
					this->face_cascade = new CascadeClassifierUnm();
					this->face_cascade->setFaceDetector();
					this->facemark = new FaceLandmarkUnm();
					this->facetracker = new FaceTrackingUnm();
					this->facetracker->setFaceTracker(1);
					this->facemark->setFaceLandmark();
				}
	}
}