# mkdir venv
# python -m venv venv
# source venv/bin/activate
# pip install -r requirements.txt
# python upload.py "com.package.name" "path/to.aab"

import sys
from apiclient import sample_tools
from oauth2client import client
import mimetypes

mimetypes.add_type("application/octet-stream", ".aab")
import http

http.DEFAULT_HTTP_TIMEOUT_SEC = 60000
TRACK = "internal"  # Can be 'internal', 'alpha', beta', 'production' or 'rollout'


def main(argv):
    service, flags = sample_tools.init(
        argv,
        "androidpublisher",
        "v3",
        __doc__,
        __file__,
        #parents=[],
        scope="https://www.googleapis.com/auth/androidpublisher",
    )

    package_name = argv[1]
    aab_file = argv[2]
    print(package_name, aab_file)
    return

    try:
        edit_request = service.edits().insert(body={}, packageName=package_name)
        result = edit_request.execute()
        edit_id = result["id"]

        aab_response = (
            service.edits()
            .bundles()
            .upload(editId=edit_id, packageName=package_name, media_body=aab_file)
            .execute()
        )

        print("Version code %d has been uploaded" % aab_response["versionCode"])

        track_response = (
            service.edits()
            .tracks()
            .update(
                editId=edit_id,
                track=TRACK,
                packageName=package_name,
                body={
                    "releases": [
                        {
                            "name": "My first API release",
                            "versionCodes": [str(aab_response["versionCode"])],
                            "status": "completed",
                        }
                    ]
                },
            )
            .execute()
        )

        print(
            "Track %s is set with releases: %s"
            % (track_response["track"], str(track_response["releases"]))
        )

        commit_request = (
            service.edits().commit(editId=edit_id, packageName=package_name).execute()
        )

        print('Edit "%s" has been committed' % (commit_request["id"]))

    except client.AccessTokenRefreshError:
        print(
            "The credentials have been revoked or expired, please re-run the "
            "application to re-authorize"
        )


if __name__ == "__main__":
    main(sys.argv)
