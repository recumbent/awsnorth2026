import { S3Client, CopyObjectCommand, DeleteObjectCommand } from "@aws-sdk/client-s3";
import { S3Event } from "aws-lambda";

const s3 = new S3Client({});

export const handler = async (event: S3Event) => {
  for (const record of event.Records) {
    const bucket = record.s3.bucket.name;
    const key = decodeURIComponent(record.s3.object.key.replace(/\+/g, " "));

    // Only move files written to the root of the bucket
    if (key.includes("/")) continue;

    const prefix = new Date().toISOString().slice(0, 16).replace(/[-T:]/g, "/"); // yyyy/mm/dd/hh/mm

    await s3.send(new CopyObjectCommand({
      Bucket: bucket,
      CopySource: `${bucket}/${encodeURIComponent(key)}`,
      Key: `${prefix}/${key}`,
    }));

    await s3.send(new DeleteObjectCommand({ Bucket: bucket, Key: key }));
  }
};
