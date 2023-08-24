**Download Putty. Open it and add the following configuration**

1. In "Session" tab type the Host Name: 18.234.9.173 on 22th port. Name it CohereDev in "Saved Sessions" and press "Save" button.
2. After that go on to the SSH/Auth tab and add cohere-dev.ppk file to "Private key file for authentication".
3. Then go to SSH/Tunnels and add the following data: Destination: **localhost:27017**, Source port **29473**. Press "Add" button. After that under the "Forwarded ports:" you should have "L29473 localhost:27017".
4. Then go on to Connection/Data and enter to "Auto-login username" value: **ubuntu**.
5. Get back to the "Session" and press "Save" button again.

---

## Configure AWS

1. Follow the path: "C:\Users\'username' and create folder **.aws**.
2. Add file **config** (without extension). Insert following lines :
    [default] 
    region = us-east-1 
    [development] 
    [profile development] 
    region = us-east-1

3. Add file **credentials** (without extension). Insert following lines :
    [development] 
    aws_access_key_id= 
    aws_secret_access_key= 
    region=us-east-1 
    toolkit_artifact_guid= 

---

## Add secrets.json

1. Follow the path "C:\Users\'username'\AppData\Roaming\Microsoft\UserSecrets.
2. Connect to the Putty tunnel and run the application for the first time. After that there should appear folder with name: **02014194-3710-49af-9ac3-5274131f793d**. If not - create it manually.
3. Add **secrets.json** to the **02014194-3710-49af-9ac3-5274131f793d** folder.

---

## Download and configure MongoDB

Connection string: **mongodb://localhost:29473/?readPreference=primary&appname=MongoDB%20Compass&ssl=false**.
Using open tunnel from Putty you can start your application and also work with dev database using MongoDbCompass 