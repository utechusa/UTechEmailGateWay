   M S B A M L   `   `   `  ����   UTechEmailGateway"    UTechEmailGateway.MainWindow   \ XPresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35W SPresentationCore, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35R NWindowsBase, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35D 9http://schemas.microsoft.com/winfx/2006/xaml/presentation    R NSystem.Xaml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     -     � Title$  UTech Email Gateway��$	��422��$	��641�� � Icon$ 	utech.png��.���  � ColumnDefinitions �� $
��150* ��� $
��141* ��� $
��168* ��� $
��160* � � RowDefinitions �� $	��36* ��� $
��134* ��� $
��141* �.+��� -   $��label1��$��Dispatch Station Status:��$��0q�$��Center=�$��	10,0,16,0q��� -   $��lbDispatchStationID��$��0q�$��Center=�$
��Left=�$��1���� -   $��tabControl1��$��1��$��4��.@��� -   $��tabItem1��$��Receive text��.���� -   $��lvReceiveLog��$��4��$��0,5,0,-5q� �� View �� .P��� $��Radio ID�� �� Width$	 150�� �� DisplayMemberBinding �� *
FromName+�� $��Received Date��$	 141�� �� *ReceivedDatetime+�� $��Message Subject��$	 168�� �� *MessageSubject+�� $��Message Body��$	 160�� �� *MessageBody+.@��� �� -   $��tabControl2��$��2��$��4��.@��� -   $��tabItem2��$��	Send text��.���� -	   $��	lvSendLog��$��4��$��0,4.6,0.4,-5.2q� �� .P��� $��
Email From��$	 150�� �� *
FromName+�� $��Received Date��$	 141�� �� *ReceivedDatetime+�� $��Mail Subject��$	 168�� �� *MailSubject+�� $��	Mail Body��$	 160�� �� *
MailBody+.@��� �� -
   $��btnExit��$
��Exit��$��0��$��3��$��Right=�$��60��$��Center=�$��30��$��0,12,4,3q�